using System.Data.Common;
using GarageSaas.Services;
using GarageSaas.Services.Models;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlCustomerDeletionCoordination")]
public class CustomerDeletionCoordinationSqlTests
{
    private DisposableSqlDatabase? _database;
    private static bool _unsafeCleanup;
    public TestContext TestContext { get; set; } = null!;
    [TestInitialize]
    public void Initialize()
    {
        if (_unsafeCleanup) Assert.Inconclusive("Uncertain cleanup: refuse further creation.");
        _database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_CUSTOMER_DELETION_SQL_TESTS", "QuoteCoordination");
        _database.Create(); TestContext.WriteLine($"Owned disposable database: {_database.DatabaseName}");
    }
    [TestCleanup]
    public async Task Cleanup()
    {
        if (_database == null) return;
        try { await _database.DeleteAsync(); TestContext.WriteLine($"Guarded deletion completed: {_database.DatabaseName}"); }
        catch { _unsafeCleanup = true; throw; }
    }
    private DbContextOptions<SignupContext> Options(DbCommandInterceptor? interceptor = null)
    { var b = new DbContextOptionsBuilder<SignupContext>(_database!.Options()); if (interceptor != null) b.AddInterceptors(interceptor); return b.Options; }

    [DataTestMethod]
    [DataRow("before")] [DataRow("after")] [DataRow("update-after")] [DataRow("commit-uncertainty")]
    public void WorkItemRollbackAndCommitUncertainty(string fault) => CustomerDeletionCoordinationCases.Check(Options(), fault, true);

    [DataTestMethod]
    [DataRow("item")] [DataRow("quote")] [DataRow("invoice")] [DataRow("convert")]
    public void PendingReferencesCannotBypassCoordination(string operation) => CustomerDeletionCoordinationCases.Pending(Options(), operation);

    [DataTestMethod]
    [DataRow("item", false, false)] [DataRow("item", true, false)]
    [DataRow("item-edit", false, false)] [DataRow("item-edit", true, false)]
    [DataRow("modal", false, false)] [DataRow("modal", true, false)]
    [DataRow("quote", false, false)] [DataRow("quote", true, false)]
    [DataRow("quote-edit", false, false)] [DataRow("quote-edit", true, false)]
    [DataRow("invoice", false, false)] [DataRow("invoice", true, false)]
    [DataRow("legacy-invoice", false, false)] [DataRow("legacy-invoice", true, false)]
    [DataRow("invoice-edit", false, false)] [DataRow("invoice-edit", true, false)]
    [DataRow("item", true, true)] [DataRow("quote", true, true)] [DataRow("invoice", true, true)]
    public async Task CustomerKeyOrdersDeletionAndEveryReferenceWriter(string kind, bool writerFirst, bool afterSave)
    {
        var state = Seed(kind); using var gate = new Gate(); using var attempt = new CustomerAttempt();
        var first = _database!.Track(Task.Run(() =>
        {
            using var db = new GateContext(Options(), gate, afterSave);
            return writerFirst ? Write(db, kind, state) : Delete(db, state);
        }));
        Task<ServiceResult>? second = null;
        try
        {
            Assert.IsTrue(gate.Entered.Wait(TimeSpan.FromSeconds(8)), "First service did not reach its bounded save gate.");
            second = _database.Track(Task.Run(() =>
            {
                using var db = new SignupContext(Options(attempt));
                return writerFirst ? Delete(db, state) : Write(db, kind, state);
            }));
            Assert.IsTrue(attempt.Entered.Wait(TimeSpan.FromSeconds(5)), "Second service never attempted customer coordination.");
            await Task.Delay(250); Assert.IsFalse(second.IsCompleted, "Second service bypassed the customer key.");
            var other = _database.Track(Task.Run(() =>
            {
                using var db = new SignupContext(Options());
                return new WorkItemService(db).AddOrUpdateWorkItem(new WorkItem { VehicleId = state.Other.VehicleId,
                    CustomerId = state.Other.CustomerId, RepairInstructions = "Independent garage" }, state.Other.GarageId, "other");
            }));
            Assert.IsTrue((await other.WaitAsync(TimeSpan.FromSeconds(5))).Success, "Unrelated garage did not progress.");
        }
        finally { gate.Release.Set(); await _database.DrainAsync(); }
        Assert.IsTrue((await first).Success);
        var losing = await second!; Assert.IsFalse(losing.Success, losing.ErrorMessage);
        using var check = new SignupContext(Options());
        var exists = check.GarageBusinessCustomer.Any(x => x.Id == state.Customer);
        var refs = check.WorkItem.Count(x => x.CustomerId == state.Customer) + check.WorkQuote.Count(x => x.CustomerId == state.Customer)
            + check.VehicleInvoice.Count(x => x.CustomerId == state.Customer || x.GarageBusinessCustomerId == state.Customer);
        Assert.AreEqual(writerFirst, exists); Assert.AreEqual(writerFirst ? 1 : 0, refs);
        if (writerFirst) StringAssert.Contains(losing.ErrorMessage, "cannot be deleted");
        TestContext.WriteLine($"{kind}: {(writerFirst ? "writer committed, deletion rejected" : "deletion committed, writer rejected missing customer")}; other garage progressed; gate after SaveChanges={afterSave}.");
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public async Task OwnershipReassignmentAndOldCustomerDeletionHaveNoReverseKeyWait(bool reassignmentFirst)
    {
        var state = Seed("item");
        using (var seed = new SignupContext(Options()))
        {
            seed.Remove(seed.WorkItem.Single(x => x.Id == state.Main.WorkItemId));
            seed.CustomerVehicle.Single(x => x.Id == state.Main.VehicleId).GarageOwned = false;
            seed.Add(new CustomerOwnedVehicles { GarageBusinessId = state.Main.GarageId,
                GarageBusinessCustomerId = state.Main.CustomerId, VehicleId = state.Main.VehicleId, CreatedDate = DateTime.UtcNow });
            seed.SaveChanges();
        }
        using var gate = new Gate(); using var attempt = new CustomerAttempt();
        ServiceResult Reassign(SignupContext db) => new CustomerVehicleService(db, null).AddOrUpdateCustomerVehicle(
            OwnershipCases.Input(state.Main.VehicleId, state.Customer.ToString()), state.Main.GarageId, "reassign");
        ServiceResult DeleteOld(SignupContext db) => new GarageCustomersService(db, null).DeleteGarageCustomer(state.Main.CustomerId, state.Main.GarageId);
        var first = _database!.Track(Task.Run(() =>
        { using var db = new GateContext(Options(), gate, false); return reassignmentFirst ? Reassign(db) : DeleteOld(db); }));
        Task<ServiceResult>? second = null;
        try
        {
            Assert.IsTrue(gate.Entered.Wait(TimeSpan.FromSeconds(8)));
            second = _database.Track(Task.Run(() =>
            { using var db = new SignupContext(Options(attempt)); return reassignmentFirst ? DeleteOld(db) : Reassign(db); }));
            Assert.IsTrue(attempt.Entered.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(250); Assert.IsFalse(second.IsCompleted);
        }
        finally { gate.Release.Set(); await _database.DrainAsync(); }
        Assert.IsTrue((await first).Success); Assert.AreEqual(reassignmentFirst, (await second!).Success);
        using var check = new SignupContext(Options());
        Assert.IsFalse(check.GarageBusinessCustomer.Any(x => x.Id == state.Main.CustomerId));
        var links = check.CustomerOwnedVehicles.AsNoTracking().Where(x => x.VehicleId == state.Main.VehicleId).ToList();
        Assert.AreEqual(reassignmentFirst ? 1 : 0, links.Count);
        if (reassignmentFirst) Assert.AreEqual(state.Customer, links[0].GarageBusinessCustomerId);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void ExistingQuoteAndModalReferencesPreventDeletionWithoutBreakingEdits(bool conversion)
    {
        var state = Seed("quote-edit");
        using var db = new SignupContext(Options());
        var deleted = new GarageCustomersService(db, null).DeleteGarageCustomer(state.Main.CustomerId, state.Main.GarageId);
        Assert.IsFalse(deleted.Success);
        var result = conversion
            ? (ServiceResult)new VehicleInvoiceService(db).CreateFromWorkQuote(state.TargetId, state.Main.GarageId, "convert")
            : new WorkItemService(db).AddOrUpdateWorkItemForQuote(new CreateWorkItemRequest { Id = state.Main.WorkItemId,
                CustomerId = int.MaxValue, VehicleId = int.MaxValue, RepairInstructions = "Description only" }, state.Main.GarageId, "edit");
        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.IsFalse(new GarageCustomersService(db, null).DeleteGarageCustomer(state.Main.CustomerId, state.Main.GarageId).Success);
    }

    private sealed record State(SqlInvoiceGraph Main, SqlInvoiceGraph Other, int Customer, int TargetId);
    private State Seed(string kind)
    {
        using var db = new SignupContext(Options()); var a = SqlInvoiceGraph.Seed(db); var b = SqlInvoiceGraph.Seed(db);
        db.CustomerOwnedVehicles.RemoveRange(db.CustomerOwnedVehicles.AsQueryable().Where(x => x.VehicleId == a.VehicleId));
        db.CustomerVehicle.Single(x => x.Id == a.VehicleId).GarageOwned = true;
        var c = new GarageBusinessCustomer { GarageBusinessId = a.GarageId, CreatedDate = DateTime.UtcNow, GarageCustomerForename = "Deletion target" };
        db.Add(c); db.SaveChanges(); var target = 0;
        if (kind == "quote-edit")
        {
            var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(Quote(a, a.CustomerId), a.GarageId, "seed");
            Assert.IsTrue(result.Success, result.ErrorMessage); target = result.Data.WorkQuoteId;
        }
        if (kind == "invoice-edit")
        {
            var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(Invoice(a, a.CustomerId), a.GarageId, "seed");
            Assert.IsTrue(result.Success, result.ErrorMessage); target = result.Data.Id;
        }
        return new(a,b,c.Id,target);
    }
    private static CombinedWorkQuoteWorkitem Quote(SqlInvoiceGraph a, int customer, int id = 0) => new()
    { CustomerId = customer, VehicleId = a.VehicleId, WorkItemId = a.WorkItemId, WorkQuoteId = id, WorkQuoteDate = DateTime.Today };
    private static VehicleInvoice Invoice(SqlInvoiceGraph a, int customer, int id = 0) => new()
    { CustomerId = customer, GarageBusinessCustomerId = customer, VehicleId = a.VehicleId, Id = id,
        InvoiceDate = DateTime.Today, InvoiceStatus = "Pending", Paid = false };
    private static ServiceResult Delete(SignupContext db, State s) => new GarageCustomersService(db, null).DeleteGarageCustomer(s.Customer, s.Main.GarageId);
    private static ServiceResult Write(SignupContext db, string kind, State s) => kind switch
    {
        "item" or "item-edit" => new WorkItemService(db).AddOrUpdateWorkItem(new WorkItem { Id = kind == "item-edit" ? s.Main.WorkItemId : 0,
            VehicleId = s.Main.VehicleId, CustomerId = s.Customer, RepairInstructions = "Concurrent financial reference" }, s.Main.GarageId, "writer"),
        "modal" => new WorkItemService(db).AddOrUpdateWorkItemForQuote(new CreateWorkItemRequest { VehicleId = s.Main.VehicleId,
            CustomerId = s.Customer, RepairInstructions = "Modal create" }, s.Main.GarageId, "writer"),
        "quote" or "quote-edit" => new WorkQuoteService(db).AddOrUpdateWorkQuote(Quote(s.Main, s.Customer, s.TargetId), s.Main.GarageId, "writer"),
        _ => new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(kind == "legacy-invoice"
            ? new VehicleInvoice { GarageBusinessCustomerId = s.Customer, VehicleId = s.Main.VehicleId, InvoiceDate = DateTime.Today, InvoiceStatus = "Pending" }
            : Invoice(s.Main, s.Customer, s.TargetId), s.Main.GarageId, "writer")
    };
    private sealed class Gate : IDisposable
    {
        internal readonly ManualResetEventSlim Entered = new(), Release = new();
        internal void Wait() { Entered.Set(); if (!Release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Customer coordination gate"); }
        public void Dispose() { Entered.Dispose(); Release.Dispose(); }
    }
    private sealed class GateContext(DbContextOptions<SignupContext> options, Gate gate, bool after) : SignupContext(options)
    {
        private bool _gated;
        public override int SaveChanges()
        { if (!_gated && !after) { _gated = true; gate.Wait(); } var count = base.SaveChanges(); if (!_gated && after) { _gated = true; gate.Wait(); } return count; }
    }
    private sealed class CustomerAttempt : DbCommandInterceptor, IDisposable
    {
        internal readonly ManualResetEventSlim Entered = new();
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result)
        { if (command.CommandText.Contains("[GarageBusinessCustomer] WITH")) Entered.Set(); return result; }
        public void Dispose() => Entered.Dispose();
    }
}


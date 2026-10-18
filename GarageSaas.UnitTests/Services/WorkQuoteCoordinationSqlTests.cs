using System.Data.Common;
using GarageSaas.Services;
using GarageSaas.Services.Models;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// Prepared only. Separate human approval is required before enabling this fixture.
[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlQuoteCoordination")]
public class WorkQuoteCoordinationSqlTests
{
    private static DisposableSqlDatabase? _database;
    private static SqlInvoiceGraph _graph = null!;
    private static int _replacement;
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_QUOTE_COORDINATION_SQL_TESTS", "QuoteCoordination");
        _database.Create();
        using var db = new SignupContext(_database.Options()); _graph = SqlInvoiceGraph.Seed(db);
        var item = new WorkItem { GarageBusinessCustomerId = _graph.GarageId, VehicleId = _graph.VehicleId,
            CustomerId = _graph.CustomerId, CreatedBy = "test", CreatedDate = DateTime.UtcNow, RepairInstructions = "Replacement" };
        db.WorkItem.Add(item); db.SaveChanges(); _replacement = item.Id;
    }
    [ClassCleanup]
    public static async Task Cleanup() { if (_database != null) await _database.DeleteAsync(); }
    [TestInitialize]
    public async Task Before() { if (_database != null) await _database.DrainAsync(); }
    [TestCleanup]
    public async Task After() { if (_database != null) await _database.DrainAsync(); }

    private static DbContextOptions<SignupContext> Options(DbCommandInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<SignupContext>(_database!.Options());
        if (interceptor != null) builder.AddInterceptors(interceptor); return builder.Options;
    }
    private static CombinedWorkQuoteWorkitem Input(int id = 0, bool edit = false) => new()
    {
        WorkQuoteId = id, CustomerId = _graph.CustomerId, VehicleId = _graph.VehicleId,
        WorkItemIds = new() { edit ? _replacement : _graph.WorkItemId },
        WorkQuoteDate = DateTime.Today, Comment = edit ? "edited" : "original", Labour = edit ? 200 : 100
    };
    private static int Quote()
    {
        using var db = new SignupContext(Options());
        var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(Input(), _graph.GarageId, "creator");
        Assert.IsTrue(result.Success, result.ErrorMessage); return result.Data.WorkQuoteId;
    }
    private static ServiceResult Modify(SignupContext db, int id, bool delete, bool retarget = true)
    {
        if (delete) return new WorkQuoteService(db).DeleteWorkQuote(id, _graph.GarageId);
        var input = Input(id, true);
        if (!retarget) input.WorkItemIds = new() { _graph.WorkItemId, _graph.WorkItemId };
        return new WorkQuoteService(db).AddOrUpdateWorkQuote(input, _graph.GarageId, "editor");
    }

    [DataTestMethod]
    [DataRow(false, false, true)] [DataRow(false, true, true)]
    [DataRow(true, false, true)] [DataRow(true, true, true)] [DataRow(false, true, false)]
    public async Task ConversionAndModificationSerializeWithLinkedRelationshipProtection(bool delete, bool conversionFirst, bool retarget)
    {
        var id = Quote(); using var saved = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        using var attempted = new LockAttempt();
        var first = _database!.Track(Task.Run(() =>
        {
            using var db = new GateContext(Options()) { GateSave = conversionFirst ? 3 : 1 };
            db.AfterSave = () => { saved.Set(); if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("First writer gate"); };
            return conversionFirst ? (ServiceResult)new VehicleInvoiceService(db).CreateFromWorkQuote(id, _graph.GarageId, "converter") : Modify(db, id, delete, retarget);
        }));
        Task<ServiceResult>? second = null;
        try
        {
            Assert.IsTrue(saved.Wait(TimeSpan.FromSeconds(10)), "First writer never reached its save");
            second = _database.Track(Task.Run(() =>
            {
                using var db = new SignupContext(Options(attempted));
                return conversionFirst ? Modify(db, id, delete, retarget) :
                    (ServiceResult)new VehicleInvoiceService(db).CreateFromWorkQuote(id, _graph.GarageId, "converter");
            }));
            Assert.IsTrue(attempted.Started.Wait(TimeSpan.FromSeconds(5)), "Second writer never attempted its quote read/lock");
            await Task.Delay(300); Assert.IsFalse(second.IsCompleted, "Second writer bypassed the quote lock");
        }
        finally { release.Set(); await _database.DrainAsync(); }
        Assert.IsTrue((await first.WaitAsync(TimeSpan.FromSeconds(10))).Success);
        Assert.IsNotNull(second); var result = await second.WaitAsync(TimeSpan.FromSeconds(10));
        using var check = new SignupContext(Options());
        if (delete && !conversionFirst)
        {
            Assert.IsFalse(result.Success); Assert.AreEqual("Work quote not found.", result.ErrorMessage);
            Assert.IsFalse(check.VehicleInvoice.Any(x => x.WorkQuoteId == id));
            Assert.IsFalse(check.InvoiceWorkQuote.Any(x => x.WorkQuoteId == id));
        }
        else if (conversionFirst && (delete || retarget))
        {
            // C3a supersedes the C2b allowance for deleting/retargeting linked quotes.
            Assert.IsFalse(result.Success);
            Assert.AreEqual(delete ? "Invoice-linked work quotes cannot be deleted." :
                "Invoice-linked work quote relationships cannot be changed.", result.ErrorMessage);
        }
        else Assert.IsTrue(result.Success, result.ErrorMessage);
        if (delete && !conversionFirst)
        {
            Assert.IsFalse(check.WorkQuote.Any(x => x.Id == id)); Assert.IsFalse(check.WorkQuoteWorkItem.Any(x => x.WorkQuoteId == id));
        }
        else
        {
            var rejected = conversionFirst && (delete || retarget);
            var quote = check.WorkQuote.Single(x => x.Id == id);
            Assert.AreEqual(rejected ? "original" : "edited", quote.Comment);
            if (rejected) { Assert.IsNull(quote.UpdatedBy); Assert.IsNull(quote.UpdatedDate); }
            Assert.AreEqual(conversionFirst ? _graph.WorkItemId : _replacement,
                check.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == id).WorkItemId);
            Assert.AreEqual(1, check.InvoiceWorkQuote.Count(x => x.WorkQuoteId == id));
            var invoice = check.VehicleInvoice.Single(x => x.WorkQuoteId == id);
            Assert.AreEqual(conversionFirst ? 100m : 200m, invoice.Labour);
            Assert.IsTrue(new VehicleInvoiceService(check).GetVehicleInvoice(invoice.Id, _graph.GarageId).Success);
        }
    }

    [DataTestMethod]
    [DataRow(false, false)] [DataRow(false, true)] [DataRow(true, false)] [DataRow(true, true)]
    public void FailedSaveRestoresStoredGraphAndLaterSaveCannotResurrectChanges(bool delete, bool after)
    {
        var id = Quote(); using var db = new GateContext(Options()) { FailSave = true, FailAfter = after };
        Assert.ThrowsException<DbUpdateException>(() => Modify(db, id, delete));
        Assert.IsFalse(db.ChangeTracker.HasChanges()); Assert.AreEqual(0, db.ChangeTracker.Entries<WorkQuote>().Count());
        Assert.AreEqual(0, db.ChangeTracker.Entries<WorkQuoteWorkItem>().Count());
        db.FailSave = false; db.SaveChanges();
        using var check = new SignupContext(Options()); var quote = check.WorkQuote.Single(x => x.Id == id);
        Assert.AreEqual("original", quote.Comment); Assert.IsNull(quote.UpdatedBy); Assert.IsNull(quote.UpdatedDate);
        Assert.AreEqual(_graph.WorkItemId, check.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == id).WorkItemId);
    }

    [TestMethod]
    public void StaleTrackedGraphIsRefreshedAndMissingReplacementDoesNotWrite()
    {
        var id = Quote(); using var db = new SignupContext(Options());
        var stale = db.WorkQuote.Single(x => x.Id == id); var staleLink = db.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == id);
        using (var other = new SignupContext(Options()))
        {
            Assert.IsTrue(Modify(other, id, false).Success);
            other.WorkQuote.Single(x => x.Id == id).CreatedBy = "fresh"; other.SaveChanges();
        }
        var invalid = Input(id); invalid.WorkItemIds = new() { int.MaxValue };
        Assert.IsFalse(new WorkQuoteService(db).AddOrUpdateWorkQuote(invalid, _graph.GarageId, "bad").Success);
        Assert.IsTrue(Modify(db, id, false).Success);
        Assert.AreEqual(EntityState.Detached, db.Entry(stale).State); Assert.AreEqual(EntityState.Detached, db.Entry(staleLink).State);
        using var check = new SignupContext(Options());
        Assert.AreEqual("fresh", check.WorkQuote.Single(x => x.Id == id).CreatedBy);
        Assert.AreEqual(_replacement, check.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == id).WorkItemId);
    }

    private sealed class GateContext : SignupContext
    {
        public int Saves, GateSave;
        public bool FailSave, FailAfter;
        public Action? AfterSave;
        public GateContext(DbContextOptions<SignupContext> options) : base(options) { }
        public override int SaveChanges()
        {
            Saves++; if (FailSave && !FailAfter) throw new DbUpdateException("Before modification save");
            var result = base.SaveChanges();
            if (FailSave && FailAfter) throw new DbUpdateException("After modification save");
            if (Saves == GateSave) AfterSave?.Invoke(); return result;
        }
    }
    private sealed class LockAttempt : DbCommandInterceptor, IDisposable
    {
        public ManualResetEventSlim Started { get; } = new();
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            // D3a conversion classifies the customer before taking its quote update lock.
            // That ordinary quote read already waits behind an uncommitted edit/delete.
            if (command.CommandText.Contains("[WorkQuote]")) Started.Set();
            return result;
        }
        public void Dispose() => Started.Dispose();
    }
}

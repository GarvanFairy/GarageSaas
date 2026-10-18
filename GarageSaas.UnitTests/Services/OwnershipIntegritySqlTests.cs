using System.Data.Common;
using GarageSaas.Services;
using GarageSaas.Services.Models;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlOwnershipIntegrity")]
public class OwnershipIntegritySqlTests
{
    private DisposableSqlDatabase? _database;
    private static bool _unsafeCleanup;
    public TestContext TestContext { get; set; } = null!;
    [TestInitialize]
    public void Initialize()
    {
        if (_unsafeCleanup) Assert.Inconclusive("Uncertain cleanup: no more database creation.");
        _database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_OWNERSHIP_INTEGRITY_SQL_TESTS", "QuoteCoordination");
        _database.Create(); TestContext.WriteLine($"Owned disposable database: {_database.DatabaseName}");
    }
    [TestCleanup]
    public async Task Cleanup()
    {
        if (_database == null) return;
        try { await _database.DeleteAsync(); TestContext.WriteLine($"Guarded deletion completed: {_database.DatabaseName}"); }
        catch { _unsafeCleanup = true; throw; }
    }
    [DataTestMethod]
    [DataRow("reassign")] [DataRow("to-garage")] [DataRow("from-garage")]
    [DataRow("duplicate")] [DataRow("two-owners")] [DataRow("foreign-label")]
    [DataRow("foreign-owner")] [DataRow("dangling-owner")] [DataRow("contradictory")]
    [DataRow("foreign-target")] [DataRow("foreign-selected")]
    [DataRow("delete")] [DataRow("delete-invoice")] [DataRow("delete-legacy-invoice")]
    [DataRow("delete-quote")] [DataRow("delete-item")]
    [DataRow("delete-duplicate")] [DataRow("delete-foreign-label")]
    [DataRow("delete-foreign-vehicle")] [DataRow("delete-dangling-vehicle")]
    [DataRow("delete-contradictory")] [DataRow("delete-foreign-target")]
    [DataRow("reassign-fail-before")] [DataRow("reassign-fail-after")]
    [DataRow("delete-fail-before")] [DataRow("delete-fail-after")]
    public void RelationalOwnershipRulesAndRollback(string scenario) => OwnershipCases.Check(_database!.Options(), scenario, true);

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public async Task CompetingReassignmentsRefreshAfterVehicleLock(bool garageFirst)
    {
        SqlInvoiceGraph a,b; int replacement;
        using (var seed = new SignupContext(_database!.Options()))
        {
            a = SqlInvoiceGraph.Seed(seed); b = SqlInvoiceGraph.Seed(seed);
            var c = new GarageBusinessCustomer { GarageBusinessId = a.GarageId, CreatedDate = DateTime.UtcNow };
            seed.Add(c); seed.SaveChanges(); replacement = c.Id;
        }
        using var gate = new Gate(); using var attempt = new LockAttempt();
        var first = _database!.Track(Task.Run(() =>
        {
            using var db = new GateContext(_database.Options(), gate, false);
            return new CustomerVehicleService(db, null).AddOrUpdateCustomerVehicle(OwnershipCases.Input(a.VehicleId, garageFirst ? "0" : replacement.ToString()), a.GarageId, "first");
        }));
        Task<ServiceResult>? second = null;
        try
        {
            Assert.IsTrue(gate.Entered.Wait(TimeSpan.FromSeconds(8)));
            second = _database.Track(Task.Run(() =>
            {
                var builder = new DbContextOptionsBuilder<SignupContext>(_database.Options()); builder.AddInterceptors(attempt); var options = builder.Options;
                using var db = new SignupContext(options);
                // Deliberately pretrack the pre-commit graph.
                db.CustomerVehicle.Single(x => x.Id == a.VehicleId);
                db.CustomerOwnedVehicles.AsQueryable().Where(x => x.VehicleId == a.VehicleId).ToList();
                return new CustomerVehicleService(db, null).AddOrUpdateCustomerVehicle(OwnershipCases.Input(a.VehicleId, garageFirst ? replacement.ToString() : "0"), a.GarageId, "second");
            }));
            Assert.IsTrue(attempt.Entered.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(250); Assert.IsFalse(second.IsCompleted, "Same-vehicle reassignment must wait for the authoritative lock.");
            var otherTask = _database.Track(Task.Run(() =>
            {
                using var other = new SignupContext(_database.Options());
                return new CustomerVehicleService(other, null)
                    .AddOrUpdateCustomerVehicle(OwnershipCases.Input(b.VehicleId, "0"), b.GarageId, "other");
            }));
            Assert.IsTrue((await otherTask.WaitAsync(TimeSpan.FromSeconds(5))).Success, "Unrelated garage must progress while only target row locks are held.");
        }
        finally { gate.Release.Set(); await _database.DrainAsync(); }
        Assert.IsTrue((await first).Success); Assert.AreEqual(garageFirst, (await second!).Success);
        using var check = new SignupContext(_database.Options());
        Assert.IsFalse(check.CustomerVehicle.Single(x => x.Id == a.VehicleId).GarageOwned == true);
        var links = check.CustomerOwnedVehicles.AsQueryable().Where(x => x.VehicleId == a.VehicleId).ToList();
        Assert.AreEqual(1, links.Count);
        Assert.AreEqual(replacement, links[0].GarageBusinessCustomerId);
    }

    [TestMethod]
    public void ReproducesPreD3MissingOwnershipLinkRace()
    {
        SqlInvoiceGraph a; int otherCustomer;
        using (var seed = new SignupContext(_database!.Options()))
        {
            a = SqlInvoiceGraph.Seed(seed);
            seed.CustomerOwnedVehicles.RemoveRange(seed.CustomerOwnedVehicles);
            seed.CustomerVehicle.Single(x => x.Id == a.VehicleId).GarageOwned = true;
            var c = new GarageBusinessCustomer { GarageBusinessId = a.GarageId, CreatedDate = DateTime.UtcNow };
            seed.Add(c); seed.SaveChanges(); otherCustomer = c.Id;
        }
        // Reconstruct just the previous unlocked read/insert branch. Both independent
        // contexts observe no ownership link before either saves; D3 prevents this schedule.
        using var first = new SignupContext(_database!.Options());
        using var second = new SignupContext(_database.Options());
        foreach (var db in new[] { first, second })
        {
            var vehicle = db.CustomerVehicle.Single(x => x.Id == a.VehicleId);
            Assert.IsFalse(db.CustomerOwnedVehicles.Any(x => x.VehicleId == a.VehicleId));
            vehicle.GarageOwned = false;
            db.Add(new CustomerOwnedVehicles { VehicleId = a.VehicleId, GarageBusinessId = a.GarageId,
                GarageBusinessCustomerId = db == first ? a.CustomerId : otherCustomer, CreatedDate = DateTime.UtcNow });
        }
        first.SaveChanges(); second.SaveChanges();
        using var check = new SignupContext(_database.Options());
        Assert.AreEqual(2, check.CustomerOwnedVehicles.Count(x => x.VehicleId == a.VehicleId));
        TestContext.WriteLine("PRE-D3 REPRODUCTION: two absent-link reads followed by separate saves create contradictory owners. Test-local reconstruction only; current service is exercised by the coordinated tests.");
    }

    [TestMethod]
    public async Task DeletionFirstNowRejectsPreviouslyRacingFinancialWriter()
    {
        SqlInvoiceGraph a;
        using (var seed = new SignupContext(_database!.Options()))
        { a = SqlInvoiceGraph.Seed(seed); seed.WorkItem.RemoveRange(seed.WorkItem); seed.SaveChanges(); }
        using var gate = new Gate();
        var deletion = _database!.Track(Task.Run(() =>
        {
            using var db = new GateContext(_database.Options(), gate, false);
            return new GarageCustomersService(db, null).DeleteGarageCustomer(a.CustomerId, a.GarageId);
        }));
        Task<ServiceResult<WorkItem>>? writer = null;
        try
        {
            Assert.IsTrue(gate.Entered.Wait(TimeSpan.FromSeconds(8)), "Deletion must finish reference validation before the writer starts.");
            writer = _database.Track(Task.Run(() =>
            {
                using var db = new SignupContext(_database.Options());
                return new WorkItemService(db).AddOrUpdateWorkItem(new WorkItem { VehicleId = a.VehicleId, CustomerId = a.CustomerId, RepairInstructions = "Concurrent reference" }, a.GarageId, "writer");
            }));
            await Task.Delay(250); Assert.IsFalse(writer.IsCompleted, "Writer must wait at the deleted customer key.");
        }
        finally { gate.Release.Set(); await _database.DrainAsync(); }
        Assert.IsTrue((await deletion).Success);
        using var check = new SignupContext(_database.Options());
        Assert.IsFalse(check.GarageBusinessCustomer.Any(x => x.Id == a.CustomerId));
        Assert.IsFalse((await writer!).Success);
        Assert.IsFalse(check.WorkItem.Any(x => x.CustomerId == a.CustomerId));
        TestContext.WriteLine("D3a regression: financial writer now waits for deletion and rejects the missing customer; no orphan remains.");
    }

    private sealed class Gate : IDisposable
    {
        internal readonly ManualResetEventSlim Entered = new(), Release = new();
        internal void Wait() { Entered.Set(); if (!Release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Bounded ownership gate"); }
        public void Dispose() { Entered.Dispose(); Release.Dispose(); }
    }
    private sealed class GateContext(DbContextOptions<SignupContext> options, Gate gate, bool after) : SignupContext(options)
    {
        public override int SaveChanges() { if (!after) gate.Wait(); var count = base.SaveChanges(); if (after) gate.Wait(); return count; }
    }
    private sealed class LockAttempt : DbCommandInterceptor, IDisposable
    {
        internal readonly ManualResetEventSlim Entered = new();
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result)
        { if (command.CommandText.Contains("[CustomerVehicle] WITH (UPDLOCK")) Entered.Set(); return result; }
        public void Dispose() => Entered.Dispose();
    }
}


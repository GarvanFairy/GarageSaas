using GarageSaas.Services;
using GarageSaas.Services.Models;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// Investigation/characterisation, NOT assertions that the current policy is ideal.
// No application catalog is opened. Dedicated opt-in; one owned DB per case.
[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlWorkItemQuoteRace")]
public class WorkItemQuoteLinkRaceSqlTests
{
    private DisposableSqlDatabase? _database;
    private SqlInvoiceGraph _a = null!, _b = null!;
    private int _vehicle, _customer;
    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_WORKITEM_QUOTE_RACE_SQL_TESTS", "QuoteCoordination");
        _database.Create();
        TestContext.WriteLine($"Owned disposable database: {_database.DatabaseName}");
        using var db = new SignupContext(_database.Options());
        _a = SqlInvoiceGraph.Seed(db); _b = SqlInvoiceGraph.Seed(db);
        var customer = new GarageBusinessCustomer { GarageBusinessId = _a.GarageId,
            GarageCustomerForename = "Replacement", CreatedBy = "test", CreatedDate = DateTime.UtcNow };
        var vehicle = new CustomerVehicle { GarageBusinessId = _a.GarageId, GarageOwned = true,
            VehicleRegistration = "REPLACEMENT", VehicleMileageDate = DateTime.UtcNow,
            CreatedBy = "test", CreatedDate = DateTime.UtcNow };
        db.AddRange(customer, vehicle); db.SaveChanges(); _vehicle = vehicle.Id; _customer = customer.Id;
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_database == null) return;
        await _database.DeleteAsync();
        TestContext.WriteLine($"Guarded deletion completed: {_database.DatabaseName}");
    }

    private CombinedWorkQuoteWorkitem Input(int id = 0, int? item = null) => new()
    {
        WorkQuoteId = id, CustomerId = _a.CustomerId, VehicleId = _a.VehicleId,
        WorkItemIds = new() { item ?? _a.WorkItemId }, WorkQuoteDate = DateTime.Today, Labour = 100
    };
    private WorkItem Replacement(int? vehicle = null, int? customer = null) => new()
    {
        Id = _a.WorkItemId, VehicleId = vehicle ?? _vehicle, CustomerId = customer ?? _customer,
        GarageBusinessCustomerId = _b.GarageId, // forged label is ignored, not assigned
        RepairInstructions = "Retargeted"
    };
    private int ExistingQuote(bool update)
    {
        if (!update) return 0;
        using var db = new SignupContext(_database!.Options());
        var spare = new WorkItem { GarageBusinessCustomerId = _a.GarageId, CustomerId = _a.CustomerId,
            VehicleId = _a.VehicleId, CreatedBy = "test", CreatedDate = DateTime.UtcNow };
        db.WorkItem.Add(spare); db.SaveChanges();
        var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(Input(item: spare.Id), _a.GarageId, "seed");
        Assert.IsTrue(result.Success, result.ErrorMessage); return result.Data.WorkQuoteId;
    }

    [DataTestMethod]
    [DataRow(false, false, "none")]
    [DataRow(true, false, "none")]
    [DataRow(false, true, "none")]
    [DataRow(true, true, "none")]
    [DataRow(false, false, "direct")]
    [DataRow(false, false, "mixed")]
    public async Task UnlinkedItemAndQuoteCanBothCommitAfterStaleValidation(
        bool update, bool quoteValidatesFirst, string invoiceMode)
    {
        var quoteId = ExistingQuote(update);
        using var reached = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var first = _database!.Track(Task.Run(() =>
        {
            using var db = new GateContext(_database.Options())
            {
                GateSave = quoteValidatesFirst && !update ? 2 : 1,
                BeforeSave = () => { reached.Set(); if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Validation gate"); }
            };
            if (quoteValidatesFirst)
                return (ServiceResult)new WorkQuoteService(db).AddOrUpdateWorkQuote(Input(quoteId), _a.GarageId, "quote-first");
            using var tx = db.Database.BeginTransaction();
            var result = new WorkItemService(db).AddOrUpdateWorkItem(Replacement(), _a.GarageId, "retarget-first");
            tx.Commit(); return (ServiceResult)result;
        }));
        int? invoiceId = null;
        try
        {
            Assert.IsTrue(reached.Wait(TimeSpan.FromSeconds(10)), "First service did not reach its post-validation gate");
            // Proves independent Garage B work progresses while A's operation is paused.
            var otherGarage = _database.Track(Task.Run(() =>
            {
                using var db = new SignupContext(_database.Options());
                return new WorkItemService(db).AddOrUpdateWorkItem(new WorkItem { Id = _b.WorkItemId,
                    VehicleId = _b.VehicleId, CustomerId = null, RepairInstructions = "Garage B progressed" }, _b.GarageId, "B");
            }));
            Assert.IsTrue((await otherGarage.WaitAsync(TimeSpan.FromSeconds(5))).Success);
            var second = _database.Track(Task.Run(() =>
            {
                using var db = new SignupContext(_database.Options());
                if (quoteValidatesFirst)
                    return (ServiceResult)new WorkItemService(db).AddOrUpdateWorkItem(Replacement(), _a.GarageId, "retarget-second");
                var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(Input(quoteId), _a.GarageId, "quote-second");
                if (result.Success) quoteId = result.Data.WorkQuoteId;
                if (result.Success && invoiceMode != "none")
                {
                    var invoiceService = new VehicleInvoiceService(db);
                    var invoice = invoiceMode == "mixed"
                        ? invoiceService.CreateFromWorkQuote(quoteId, _a.GarageId, "converter")
                        : invoiceService.AddOrUpdateVehicleInvoice(new VehicleInvoice { GarageBusinessId = _a.GarageId,
                            CustomerId = _a.CustomerId, GarageBusinessCustomerId = _a.CustomerId,
                            VehicleId = _a.VehicleId, WorkQuoteId = quoteId, InvoiceDate = DateTime.Today }, _a.GarageId, "manual");
                    Assert.IsTrue(invoice.Success, invoice.ErrorMessage); invoiceId = invoice.Data.Id;
                }
                return (ServiceResult)result;
            }));
            Assert.IsTrue((await second.WaitAsync(TimeSpan.FromSeconds(8))).Success,
                "Second operation could not commit while the first was paused");
        }
        finally { release.Set(); await _database.DrainAsync(); }
        Assert.IsTrue((await first).Success);
        using var check = new SignupContext(_database.Options());
        var item = check.WorkItem.AsNoTracking().Single(x => x.Id == _a.WorkItemId);
        Assert.AreEqual(_vehicle, item.VehicleId); Assert.AreEqual(_customer, item.CustomerId);
        Assert.AreEqual(_a.GarageId, item.GarageBusinessCustomerId, "Forged garage label must not retarget tenant");
        var link = check.WorkQuoteWorkItem.AsNoTracking().Single(x => x.WorkItemId == _a.WorkItemId);
        var quote = check.WorkQuote.AsNoTracking().Single(x => x.Id == link.WorkQuoteId);
        Assert.AreEqual(_a.VehicleId, quote.VehicleId); Assert.AreEqual(_a.CustomerId, quote.CustomerId);
        Assert.IsTrue(new WorkQuoteService(check).GetWorkQuote(quote.Id, _a.GarageId).Success,
            "Current validation is garage tenancy, not equal customer/vehicle IDs");
        if (invoiceId.HasValue)
            Assert.IsTrue(new VehicleInvoiceService(check).GetVehicleInvoice(invoiceId.Value, _a.GarageId).Success);
        Assert.IsFalse(new WorkItemService(check).GetWorkItem(item.Id, _b.GarageId).Success);
        TestContext.WriteLine($"Both succeeded; quote {quote.Id} kept original customer/vehicle, item {item.Id} moved within garage A. Invoice mode={invoiceMode}.");
    }

    [DataTestMethod]
    [DataRow("direct")] [DataRow("join")] [DataRow("mixed")] [DataRow("malformed")]
    public void CommittedQuoteLinkAlreadyBlocksRetargetingForEveryInvoiceRepresentation(string representation)
    {
        using var db = new SignupContext(_database!.Options());
        var quote = new WorkQuoteService(db).AddOrUpdateWorkQuote(Input(), _a.GarageId, "seed");
        Assert.IsTrue(quote.Success, quote.ErrorMessage);
        var invoice = new VehicleInvoice { GarageBusinessId = _a.GarageId, CustomerId = _a.CustomerId,
            VehicleId = _a.VehicleId, WorkQuoteId = representation == "join" ? null : quote.Data.WorkQuoteId,
            CreatedDate = DateTime.UtcNow, CreatedBy = "historical" };
        db.VehicleInvoice.Add(invoice); db.SaveChanges();
        if (representation != "direct")
        {
            db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = representation == "malformed" ? null : invoice.Id,
                WorkQuoteId = quote.Data.WorkQuoteId, GarageBusinessCustomerId = representation == "malformed" ? _b.GarageId : _a.GarageId,
                CreatedDate = DateTime.UtcNow, CreatedBy = "historical" }); db.SaveChanges();
        }
        var before = db.WorkItem.AsNoTracking().Single(x => x.Id == _a.WorkItemId);
        var result = new WorkItemService(db).AddOrUpdateWorkItem(Replacement(), _a.GarageId, "rejected");
        Assert.IsFalse(result.Success); Assert.AreEqual("Linked work item relationships cannot be changed.", result.ErrorMessage);
        using var check = new SignupContext(_database.Options());
        var after = check.WorkItem.Single(x => x.Id == _a.WorkItemId);
        Assert.AreEqual(before.VehicleId, after.VehicleId); Assert.AreEqual(before.CustomerId, after.CustomerId);
        Assert.AreEqual(before.UpdatedDate, after.UpdatedDate); Assert.AreEqual(before.UpdatedBy, after.UpdatedBy);
        Assert.AreEqual(1, check.VehicleInvoice.Count()); Assert.AreEqual(1, check.WorkQuoteWorkItem.Count());
    }

    [TestMethod]
    public async Task RetargetSaveFailureRollsBackWithoutRemovingTheCommittedQuote()
    {
        using var reached = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var worker = _database!.Track(Task.Run(() =>
        {
            using var db = new GateContext(_database.Options()) { GateSave = 1, FailAfterSave = 1,
                BeforeSave = () => { reached.Set(); if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Retarget rollback gate"); } };
            using var tx = db.Database.BeginTransaction();
            try
            {
                new WorkItemService(db).AddOrUpdateWorkItem(Replacement(), _a.GarageId, "failed-retarget");
                Assert.Fail("Expected injected failure");
            }
            catch (DbUpdateException) { tx.Rollback(); }
        }));
        try
        {
            Assert.IsTrue(reached.Wait(TimeSpan.FromSeconds(10)));
            var quoteWorker = _database.Track(Task.Run(() =>
            {
                using var db = new SignupContext(_database.Options());
                return new WorkQuoteService(db).AddOrUpdateWorkQuote(Input(), _a.GarageId, "committed-quote");
            }));
            Assert.IsTrue((await quoteWorker.WaitAsync(TimeSpan.FromSeconds(8))).Success);
        }
        finally { release.Set(); await _database.DrainAsync(); }
        await worker;
        using var check = new SignupContext(_database.Options());
        var item = check.WorkItem.Single(x => x.Id == _a.WorkItemId);
        Assert.AreEqual(_a.VehicleId, item.VehicleId); Assert.AreEqual(_a.CustomerId, item.CustomerId);
        Assert.IsNull(item.UpdatedDate); Assert.IsNull(item.UpdatedBy);
        Assert.AreEqual(1, check.WorkQuote.Count()); Assert.AreEqual(1, check.WorkQuoteWorkItem.Count());
    }

    [TestMethod]
    public void ForeignReplacementFailsAndQuoteFailureRollsBackBeforeRetarget()
    {
        using (var db = new SignupContext(_database!.Options()))
        {
            Assert.IsFalse(new WorkItemService(db).AddOrUpdateWorkItem(Replacement(_b.VehicleId, _b.CustomerId), _a.GarageId, "foreign").Success);
            Assert.IsFalse(db.ChangeTracker.HasChanges());
        }
        using (var db = new GateContext(_database.Options()) { FailAfterSave = 2 })
        {
            Assert.ThrowsException<DbUpdateException>(() => new WorkQuoteService(db).AddOrUpdateWorkQuote(Input(), _a.GarageId, "fail"));
            Assert.IsFalse(db.ChangeTracker.HasChanges()); db.FailAfterSave = 0; db.SaveChanges();
        }
        using var check = new SignupContext(_database.Options());
        Assert.AreEqual(0, check.WorkQuote.Count()); Assert.AreEqual(0, check.WorkQuoteWorkItem.Count());
        Assert.IsTrue(new WorkItemService(check).AddOrUpdateWorkItem(Replacement(), _a.GarageId, "valid").Success);
    }

    private sealed class GateContext : SignupContext
    {
        public int GateSave, FailAfterSave, Saves;
        public Action? BeforeSave;
        public GateContext(DbContextOptions<SignupContext> options) : base(options) { }
        public override int SaveChanges()
        {
            Saves++; if (Saves == GateSave) BeforeSave?.Invoke();
            var count = base.SaveChanges();
            if (Saves == FailAfterSave) throw new DbUpdateException("Injected after quote/link save");
            return count;
        }
    }
}

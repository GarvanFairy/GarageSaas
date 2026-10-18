using GarageSaas.UnitTests.TestHelpers;
using GarageSaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// PREPARED, OPT-IN SQL TESTS: never use application configuration/database.
// Requires explicit human approval before setting BOTH environment variables.
[TestClass]
[DoNotParallelize]
[TestCategory("SqlServer")]
[TestCategory("SqlInvoiceAtomicity")]
public class ManualInvoiceSqlTransactionTests
{
    private static DisposableSqlDatabase? _database;

    private static SqlInvoiceGraph _graph = null!;
    private static int _garageId => _graph.GarageId;

    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_INVOICE_ATOMIC_SQL_TESTS", "InvoiceAtomic");
        _database.Create();
        using var db = new SignupContext(Options());
        _graph = SqlInvoiceGraph.Seed(db);
    }

    [ClassCleanup]
    public static async Task Cleanup() { if (_database != null) await _database.DeleteAsync(); }

    [TestCleanup]
    public async Task DrainWorkers() { if (_database != null) await _database.DrainAsync(); }

    [TestInitialize]
    public async Task WaitForPreviousWorkers() { if (_database != null) await _database.DrainAsync(); }

    private static DbContextOptions<SignupContext> Options() => _database!.Options();
    private static VehicleInvoice Input() => new()
    {
        GarageBusinessId = _garageId, InvoiceDate = new DateTime(2026, 10, 17), InvoiceStatus = "Pending",
        CustomerId = _graph.CustomerId, GarageBusinessCustomerId = _graph.CustomerId, VehicleId = _graph.VehicleId,
        Comment = Guid.NewGuid().ToString("N"), Paid = false
    };

    [TestMethod]
    public void BothSavesUseOneRealTransactionAndCommitNumberTogether()
    {
        using var db = new FaultContext(Options());
        var input = Input(); var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, _garageId, "test");
        Assert.IsTrue(result.Success, result.ErrorMessage); Assert.AreEqual(2, db.SaveCalls);
        Assert.AreEqual(1, db.TransactionIds.Distinct().Count()); Assert.IsFalse(db.TransactionIds.Contains(Guid.Empty));
        Assert.IsNull(db.NumbersAtSave[0]); Assert.AreEqual($"INV-2026-{result.Data.Id:D6}", db.NumbersAtSave[1]);
        using var observed = new SignupContext(Options()); var stored = observed.VehicleInvoice.Single(x => x.Id == result.Data.Id);
        Assert.AreEqual(result.Data.InvoiceNumber, stored.InvoiceNumber); Assert.IsNull(db.Database.CurrentTransaction);
        Assert.AreEqual(_graph.CustomerId, stored.CustomerId); Assert.AreEqual(_graph.VehicleId, stored.VehicleId);
        Assert.AreEqual(0, observed.InvoiceWorkQuote.Count());
    }

    [DataTestMethod]
    [DataRow(1, false)] [DataRow(1, true)] [DataRow(2, false)] [DataRow(2, true)]
    public void FailureBeforeOrAfterEitherSaveLeavesNoInvoiceInSqlServer(int failAt, bool afterWrite)
    {
        using var db = new FaultContext(Options()) { FailAt = failAt, FailAfterWrite = afterWrite };
        using var initial = new SignupContext(Options()); var count = initial.VehicleInvoice.Count();
        var input = Input(); var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, _garageId, "test");
        Assert.IsFalse(result.Success); Assert.IsNull(result.Data); Assert.AreEqual(failAt, db.SaveCalls);
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count()); Assert.IsNull(db.Database.CurrentTransaction);
        if (db.CreatedInvoice != null) { Assert.AreEqual(0, db.CreatedInvoice.Id); Assert.IsNull(db.CreatedInvoice.InvoiceNumber); }
        using var observed = new SignupContext(Options());
        Assert.AreEqual(count, observed.VehicleInvoice.Count());
        Assert.IsFalse(observed.VehicleInvoice.Any(x => x.Comment == input.Comment));
        Assert.AreEqual(0, observed.InvoiceWorkQuote.Count());
    }

    [TestMethod]
    public void InvalidReferencesPerformNoSavesAndLeaveSqlDatabaseUnchanged()
    {
        using var db = new FaultContext(Options()); var input = Input(); input.CustomerId = input.GarageBusinessCustomerId = int.MaxValue;
        using var initial = new SignupContext(Options()); var count = initial.VehicleInvoice.Count();
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, _garageId, "test");
        Assert.IsFalse(result.Success); Assert.AreEqual(0, db.SaveCalls); Assert.IsNull(db.Database.CurrentTransaction);
        using var observed = new SignupContext(Options()); Assert.AreEqual(count, observed.VehicleInvoice.Count());
    }

    private sealed class FaultContext : SignupContext
    {
        public int SaveCalls { get; private set; }
        public int FailAt { get; set; }
        public bool FailAfterWrite { get; set; }
        public VehicleInvoice? CreatedInvoice { get; private set; }
        public List<Guid> TransactionIds { get; } = new();
        public List<string?> NumbersAtSave { get; } = new();
        public FaultContext(DbContextOptions<SignupContext> options) : base(options) { }
        public override int SaveChanges()
        {
            SaveCalls++;
            CreatedInvoice ??= ChangeTracker.Entries<VehicleInvoice>().Single().Entity;
            TransactionIds.Add(Database.CurrentTransaction?.TransactionId ?? Guid.Empty);
            NumbersAtSave.Add(CreatedInvoice.InvoiceNumber);
            if (SaveCalls == FailAt && !FailAfterWrite) throw new DbUpdateException("Injected pre-save failure.");
            var changes = base.SaveChanges();
            if (SaveCalls == FailAt && FailAfterWrite) throw new DbUpdateException("Injected post-save failure.");
            return changes;
        }
    }
}

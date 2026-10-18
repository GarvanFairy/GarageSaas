using GarageSaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// These tests assert transaction orchestration and EF cleanup, NOT SQL rollback.
// InMemory persists writes even when our mock transaction rolls back.
[TestClass]
public class ManualInvoiceTransactionTests
{
    private static ObservedContext Database()
    {
        var options = VehicleInvoiceServiceTests.Options();
        using (var seed = new SignupContext(options)) VehicleInvoiceServiceTests.Seed(seed);
        return new ObservedContext(options);
    }

    [TestMethod]
    public void TwoSavesPrecedeOneCommitAndKeepExistingNumberFormat()
    {
        using var db = Database();
        var input = VehicleInvoiceServiceTests.Invoice(0);
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, 1, "actor");
        Assert.IsTrue(result.Success, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "begin", "save1", "save2", "commit", "dispose" }, db.Events);
        Assert.AreEqual($"INV-2020-{result.Data.Id:D6}", result.Data.InvoiceNumber);
        Assert.AreEqual(0, input.Id, "The request model must not receive a generated id.");
    }

    [DataTestMethod]
    [DataRow(1)] [DataRow(2)]
    public void SaveFailureRollsBackCleansTrackerAndReturnsNoGeneratedIdentity(int save)
    {
        using var db = Database(); db.FailSave = save;
        var input = VehicleInvoiceServiceTests.Invoice(0);
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, 1, "actor");
        Assert.IsFalse(result.Success); Assert.IsNull(result.Data);
        Assert.AreEqual(save, db.SaveCalls); Assert.IsTrue(db.Events.Contains("rollback")); Assert.IsFalse(db.Events.Contains("commit"));
        Assert.AreEqual("dispose", db.Events.Last());
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
        Assert.IsNotNull(db.CreatedInvoice); Assert.AreEqual(0, db.CreatedInvoice!.Id); Assert.IsNull(db.CreatedInvoice.InvoiceNumber);
        Assert.AreEqual(0, input.Id);
        // Deliberately no row-count assertion: InMemory cannot prove rollback.
    }

    [TestMethod]
    public void ValidationRunsAfterBeginAndKeepsExistingErrorWithoutSaving()
    {
        using var db = Database();
        db.OnBegin = () =>
        {
            // Simulate the reference changing between the entry check and Begin.
            using var other = new SignupContext(db.Options);
            other.GarageBusinessCustomer.Single(x => x.Id == 101).GarageBusinessId = 2;
            other.SaveChanges();
        };
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(VehicleInvoiceServiceTests.Invoice(0), 1, "actor");
        Assert.IsFalse(result.Success); Assert.AreEqual("Invoice relationships are invalid for this garage.", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "begin", "dispose" }, db.Events);
        Assert.AreEqual(0, db.SaveCalls); Assert.AreEqual(0, db.VehicleInvoice.Count());
    }

    [TestMethod]
    public void CommitAcknowledgementFailureIsUncertainAndNeverRetries()
    {
        using var db = Database(); db.FailCommit = true;
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(VehicleInvoiceServiceTests.Invoice(0), 1, "actor");
        Assert.IsFalse(result.Success); Assert.IsNull(result.Data);
        StringAssert.Contains(result.ErrorMessage, "could not be confirmed");
        Assert.AreEqual(1, db.Events.Count(x => x == "begin")); Assert.AreEqual(1, db.Events.Count(x => x == "commit"));
        Assert.AreEqual(2, db.SaveCalls); Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
        Assert.AreEqual(0, db.CreatedInvoice!.Id); Assert.IsNull(db.CreatedInvoice.InvoiceNumber);
        // InMemory's completed writes remain: rollback does not prove an absent
        // invoice, illustrating why the result must not claim a failed commit.
        using var observed = new SignupContext(db.Options);
        Assert.AreEqual(1, observed.VehicleInvoice.Count()); Assert.IsNotNull(observed.VehicleInvoice.Single().InvoiceNumber);
    }

    [TestMethod]
    public void FailedRollbackIsUncertainAndDoesNotRetryOrLeakTheFailure()
    {
        using var db = Database(); db.FailSave = 2; db.FailRollback = true;
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(VehicleInvoiceServiceTests.Invoice(0), 1, "actor");
        Assert.IsFalse(result.Success); StringAssert.Contains(result.ErrorMessage, "could not be confirmed");
        Assert.AreEqual(2, db.SaveCalls); Assert.AreEqual(1, db.Events.Count(x => x == "begin"));
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count()); Assert.IsFalse(result.ErrorMessage.Contains("simulated"));
    }

    [TestMethod]
    public void BeginningTransactionFailureDoesNotSaveOrRetry()
    {
        using var db = Database(); db.FailBegin = true;
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(VehicleInvoiceServiceTests.Invoice(0), 1, "actor");
        Assert.IsFalse(result.Success); Assert.AreEqual(0, db.SaveCalls); Assert.AreEqual(1, db.Events.Count);
    }

    private sealed class ObservedContext : SignupContext
    {
        private readonly Mock<DatabaseFacade> _facade;
        public DbContextOptions<SignupContext> Options { get; }
        public List<string> Events { get; } = new();
        public int FailSave { get; set; }
        public int SaveCalls { get; private set; }
        public bool FailBegin { get; set; }
        public bool FailCommit { get; set; }
        public bool FailRollback { get; set; }
        public Action? OnBegin { get; set; }
        public VehicleInvoice? CreatedInvoice { get; private set; }
        public override DatabaseFacade Database => _facade?.Object ?? base.Database;
        public ObservedContext(DbContextOptions<SignupContext> options) : base(options)
        {
            Options = options;
            var transaction = new Mock<IDbContextTransaction>();
            transaction.Setup(x => x.Commit()).Callback(() => { Events.Add("commit"); if (FailCommit) throw new IOException("simulated commit acknowledgement loss"); });
            transaction.Setup(x => x.Rollback()).Callback(() => { Events.Add("rollback"); if (FailRollback) throw new IOException("simulated rollback loss"); });
            transaction.Setup(x => x.Dispose()).Callback(() => Events.Add("dispose"));
            _facade = new Mock<DatabaseFacade>(this) { CallBase = true };
            _facade.Setup(x => x.BeginTransaction()).Returns(() =>
            {
                Events.Add("begin"); if (FailBegin) throw new IOException("simulated begin failure");
                OnBegin?.Invoke(); return transaction.Object;
            });
        }
        public override int SaveChanges()
        {
            Assert.IsTrue(Events.Contains("begin")); Assert.IsFalse(Events.Contains("commit"));
            SaveCalls++; Events.Add("save" + SaveCalls);
            CreatedInvoice ??= ChangeTracker.Entries<VehicleInvoice>().Single().Entity;
            if (SaveCalls == FailSave) throw new DbUpdateException("simulated save failure");
            return base.SaveChanges();
        }
    }
}

using System.Text.Json;
using GarageSaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// Mock transactions verify orchestration, not persistence rollback or SQL locks.
[TestClass]
public class InvoiceUpdateCoordinationTests
{
    private static ObservedContext Database(string kind = "none")
    {
        var options = VehicleInvoiceServiceTests.Options();
        using (var db = new SignupContext(options))
        {
            VehicleInvoiceServiceTests.Seed(db); var invoice = VehicleInvoiceServiceTests.Invoice();
            if (kind is "direct" or "mixed" or "multi" or "duplicate-history") invoice.WorkQuoteId = 100;
            db.VehicleInvoice.Add(invoice);
            if (kind is "join" or "mixed" or "multi") db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 100, GarageBusinessCustomerId = 1 });
            if (kind == "multi") db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 101, GarageBusinessCustomerId = 1 });
            if (kind == "duplicate-history") { var other = VehicleInvoiceServiceTests.Invoice(2); other.WorkQuoteId = 100; db.VehicleInvoice.Add(other); }
            db.SaveChanges();
        }
        return new ObservedContext(options);
    }
    private static string State(SignupContext db) => JsonSerializer.Serialize(new
    {
        invoices = db.VehicleInvoice.AsNoTracking().OrderBy(x => x.Id).ToList(),
        links = db.InvoiceWorkQuote.AsNoTracking().OrderBy(x => x.Id).ToList()
    });

    [DataTestMethod]
    [DataRow("none")] [DataRow("direct")] [DataRow("join")] [DataRow("mixed")]
    [DataRow("multi")] [DataRow("duplicate-history")]
    public void OrdinaryHistoricalEditsRemainSupportedWithoutRewritingAssociations(string kind)
    {
        using var db = Database(kind); var input = VehicleInvoiceServiceTests.Invoice();
        input.WorkQuoteId = kind is "direct" or "mixed" or "multi" or "duplicate-history" ? 100 : null;
        input.Comment = "Updated"; input.Labour = 250; input.InvoiceNumber = "Editable reference";
        var links = JsonSerializer.Serialize(db.InvoiceWorkQuote.AsNoTracking().ToList());
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, 1, "editor");
        Assert.IsTrue(result.Success, result.ErrorMessage); Assert.AreEqual(250m, result.Data.Labour);
        Assert.AreEqual("Editable reference", result.Data.InvoiceNumber); Assert.AreEqual("editor", result.Data.UpdatedBy);
        Assert.AreEqual(links, JsonSerializer.Serialize(db.InvoiceWorkQuote.AsNoTracking().ToList()));
        CollectionAssert.AreEqual(new[] { "begin", "save", "commit", "dispose" }, db.Events);
    }

    [TestMethod]
    public void UnlinkedCustomerVehicleChangesAndNewEligibleAttachmentRemainSupported()
    {
        using var db = Database(); var input = VehicleInvoiceServiceTests.Invoice();
        input.CustomerId = input.GarageBusinessCustomerId = 102; input.VehicleId = 11; input.WorkQuoteId = 100;
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, 1, "editor");
        Assert.IsTrue(result.Success, result.ErrorMessage); Assert.AreEqual(100, result.Data.WorkQuoteId);
        Assert.AreEqual(0, db.InvoiceWorkQuote.Count()); Assert.AreEqual(1, db.VehicleInvoice.Count());
    }

    [DataTestMethod]
    [DataRow("direct")] [DataRow("join")] [DataRow("mixed")] [DataRow("multi")]
    [DataRow("null-invoice")] [DataRow("missing-invoice")] [DataRow("foreign-label")] [DataRow("foreign-direct")]
    [DataRow("conflict")]
    public void NewAttachmentRejectsDuplicatesAndMalformedEndpointsWithoutMutations(string kind)
    {
        using var db = Database();
        using (var seed = new SignupContext(db.Options))
        {
            var other = VehicleInvoiceServiceTests.Invoice(2);
            if (kind is "direct" or "mixed" or "foreign-direct" or "conflict") other.WorkQuoteId = 100;
            if (kind == "foreign-direct") other.GarageBusinessId = 2;
            seed.VehicleInvoice.Add(other);
            if (kind is not ("direct" or "foreign-direct")) seed.InvoiceWorkQuote.Add(new InvoiceWorkQuote
            { InvoiceId = kind == "null-invoice" ? null : kind == "missing-invoice" ? 999 : 2,
                WorkQuoteId = 100, GarageBusinessCustomerId = kind == "foreign-label" ? 2 : 1 });
            if (kind == "multi") seed.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 2, WorkQuoteId = 101, GarageBusinessCustomerId = 1 });
            if (kind == "conflict") { var third = VehicleInvoiceServiceTests.Invoice(3); third.WorkQuoteId = 100; seed.VehicleInvoice.Add(third); }
            seed.SaveChanges();
        }
        var before = State(db); var input = VehicleInvoiceServiceTests.Invoice(); input.WorkQuoteId = 100; input.Comment = "Rejected";
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, 1, "editor");
        Assert.IsFalse(result.Success); Assert.AreEqual(0, db.Saves); Assert.AreEqual(before, State(db));
        Assert.IsFalse(db.ChangeTracker.HasChanges()); Assert.IsTrue(db.Events.Contains("rollback"));
    }

    [DataTestMethod]
    [DataRow(null)] [DataRow(101)] [DataRow(0)]
    public void LinkedRemovalReplacementAndZeroStayRejected(int? id)
    {
        using var db = Database("direct"); var before = State(db); var input = VehicleInvoiceServiceTests.Invoice(); input.WorkQuoteId = id;
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, 1, "editor");
        Assert.IsFalse(result.Success); Assert.AreEqual("Linked invoice relationships cannot be changed.", result.ErrorMessage);
        Assert.AreEqual(before, State(db)); Assert.AreEqual(0, db.Saves);
    }

    [TestMethod]
    public void ZeroOnUnlinkedInvoiceRetainsInvalidRelationshipFailure()
    {
        using var db = Database(); var input = VehicleInvoiceServiceTests.Invoice(); input.WorkQuoteId = 0;
        Assert.AreEqual("Invoice relationships are invalid for this garage.", new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, 1, "editor").ErrorMessage);
        Assert.AreEqual(0, db.Saves);
    }

    [TestMethod]
    public void StaleNullRequestAndStaleTrackedInvoiceCannotClearNewAssociation()
    {
        using var db = Database(); var stale = db.VehicleInvoice.Single();
        db.OnBegin = () => { using var other = new SignupContext(db.Options); other.VehicleInvoice.Single().WorkQuoteId = 100; other.SaveChanges(); };
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(VehicleInvoiceServiceTests.Invoice(), 1, "stale");
        Assert.IsFalse(result.Success); Assert.AreEqual(0, db.Saves);
        Assert.AreEqual(100, db.VehicleInvoice.AsNoTracking().Single().WorkQuoteId);
        db.OnBegin = null; var input = VehicleInvoiceServiceTests.Invoice(); input.WorkQuoteId = 100;
        Assert.IsTrue(new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, 1, "fresh").Success);
        Assert.AreEqual(EntityState.Detached, db.Entry(stale).State);
    }

    [TestMethod]
    public void ClassificationChangeRequiringLateQuoteLockReturnsRefreshFailure()
    {
        using var db = Database();
        using (var other = new SignupContext(db.Options)) { other.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = null, GarageBusinessCustomerId = 1 }); other.SaveChanges(); }
        db.OnBegin = () => { using var other = new SignupContext(db.Options); other.InvoiceWorkQuote.RemoveRange(other.InvoiceWorkQuote.ToList()); other.SaveChanges(); };
        var input = VehicleInvoiceServiceTests.Invoice(); input.WorkQuoteId = 100;
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, 1, "editor");
        Assert.IsFalse(result.Success); StringAssert.Contains(result.ErrorMessage, "Refresh"); Assert.AreEqual(0, db.Saves);
    }

    [TestMethod]
    public void PendingEditsAreNotDiscardedOrSaved()
    {
        using var db = Database(); db.VehicleInvoice.Single().Comment = "Pending";
        Assert.IsFalse(new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(VehicleInvoiceServiceTests.Invoice(), 1, "editor").Success);
        Assert.IsTrue(db.ChangeTracker.HasChanges()); Assert.AreEqual(0, db.Events.Count);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void SaveFailureRollsBackAndDetachesAffectedInvoice(bool after)
    {
        using var db = Database(); db.FailSave = true; db.FailAfter = after;
        var input = VehicleInvoiceServiceTests.Invoice(); input.WorkQuoteId = 100;
        Assert.ThrowsException<DbUpdateException>(() => new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, 1, "editor"));
        Assert.IsTrue(db.Events.Contains("rollback")); Assert.IsFalse(db.Events.Contains("commit"));
        Assert.IsFalse(db.ChangeTracker.HasChanges()); Assert.AreEqual(0, db.ChangeTracker.Entries<VehicleInvoice>().Count());
    }

    [TestMethod]
    public void CommitUncertaintyDoesNotRetry()
    {
        using var db = Database(); db.FailCommit = true;
        var error = Assert.ThrowsException<InvalidOperationException>(() => new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(VehicleInvoiceServiceTests.Invoice(), 1, "editor"));
        StringAssert.Contains(error.Message, "could not be confirmed"); Assert.AreEqual(1, db.Saves);
        Assert.AreEqual(1, db.Events.Count(x => x == "commit")); Assert.IsFalse(db.ChangeTracker.HasChanges());
    }

    private sealed class ObservedContext : SignupContext
    {
        private readonly Mock<DatabaseFacade> _facade;
        public override DatabaseFacade Database => _facade?.Object ?? base.Database;
        public DbContextOptions<SignupContext> Options { get; }
        public List<string> Events { get; } = new(); public int Saves;
        public Action? OnBegin; public bool FailSave, FailAfter, FailCommit;
        public ObservedContext(DbContextOptions<SignupContext> options) : base(options)
        {
            Options = options; var transaction = new Mock<IDbContextTransaction>();
            transaction.Setup(x => x.Commit()).Callback(() => { Events.Add("commit"); if (FailCommit) throw new IOException("Lost acknowledgement"); });
            transaction.Setup(x => x.Rollback()).Callback(() => Events.Add("rollback"));
            transaction.Setup(x => x.Dispose()).Callback(() => Events.Add("dispose"));
            _facade = new Mock<DatabaseFacade>(this) { CallBase = true };
            _facade.Setup(x => x.BeginTransaction()).Returns(() => { Events.Add("begin"); OnBegin?.Invoke(); return transaction.Object; });
        }
        public override int SaveChanges()
        {
            Saves++; Events.Add("save"); if (FailSave && !FailAfter) throw new DbUpdateException("Before save");
            var result = base.SaveChanges(); if (FailSave && FailAfter) throw new DbUpdateException("After save"); return result;
        }
    }
}

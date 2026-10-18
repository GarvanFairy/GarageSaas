using GarageSaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// Control-flow tests only; InMemory cannot prove locks or rollback.
[TestClass]
public class QuoteConversionTransactionTests
{
    private static ObservedContext Database()
    {
        var options = VehicleInvoiceServiceTests.Options();
        using (var seed = new SignupContext(options)) VehicleInvoiceServiceTests.Seed(seed);
        return new ObservedContext(options);
    }

    [TestMethod]
    public void ConversionUsesThreeSavesThenCommitAndDuplicateDoesNotSave()
    {
        using var db = Database();
        var service = new VehicleInvoiceService(db);
        var result = service.CreateFromWorkQuote(100, 1, "actor");
        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.AreEqual($"INV-{DateTime.Now:yyyy}-{result.Data.Id:D6}", result.Data.InvoiceNumber);
        CollectionAssert.AreEqual(new[] { "begin", "save1", "save2", "save3", "commit", "dispose" }, db.Events);
        var duplicate = service.CreateFromWorkQuote(100, 1, "actor");
        Assert.IsFalse(duplicate.Success);
        Assert.AreEqual("This work quote has already been converted to an invoice.", duplicate.ErrorMessage);
        Assert.AreEqual(3, db.Saves);
    }

    [TestMethod]
    public void LegacyDirectOnlyAssociationPreventsReconversion()
    {
        using var db = Database();
        using (var other = new SignupContext(db.Options))
        {
            var invoice = VehicleInvoiceServiceTests.Invoice(); invoice.WorkQuoteId = 100;
            other.VehicleInvoice.Add(invoice); other.SaveChanges();
        }
        var result = new VehicleInvoiceService(db).CreateFromWorkQuote(100, 1, "actor");
        Assert.AreEqual("This work quote has already been converted to an invoice.", result.ErrorMessage);
        Assert.AreEqual(0, db.Saves); Assert.AreEqual(0, db.InvoiceWorkQuote.Count());
    }

    [DataTestMethod]
    [DataRow("orphan")] [DataRow("foreign-link")] [DataRow("missing-invoice")] [DataRow("conflicting")] [DataRow("foreign-direct")]
    public void MalformedAssociationsAreRejectedWithoutSaves(string kind)
    {
        using var db = Database();
        using (var other = new SignupContext(db.Options))
        {
            var invoice = VehicleInvoiceServiceTests.Invoice(); invoice.WorkQuoteId = 100;
            if (kind == "foreign-direct") invoice.GarageBusinessId = 2;
            other.VehicleInvoice.Add(invoice);
            if (kind != "foreign-direct")
                other.InvoiceWorkQuote.Add(new InvoiceWorkQuote { WorkQuoteId = 100,
                    InvoiceId = kind == "orphan" ? null : kind == "missing-invoice" ? 999 : 1,
                    GarageBusinessCustomerId = kind == "foreign-link" ? 2 : 1 });
            if (kind == "conflicting")
            {
                var second = VehicleInvoiceServiceTests.Invoice(2); second.WorkQuoteId = 100;
                other.VehicleInvoice.Add(second);
            }
            other.SaveChanges();
        }
        var result = new VehicleInvoiceService(db).CreateFromWorkQuote(100, 1, "actor");
        Assert.AreEqual("Invoice relationships are invalid for this garage.", result.ErrorMessage);
        Assert.AreEqual(0, db.Saves);
    }

    [DataTestMethod]
    [DataRow(200)] [DataRow(999)]
    public void ForeignAndMissingQuotesHaveSameSafeResponse(int id)
    {
        using var db = Database();
        var result = new VehicleInvoiceService(db).CreateFromWorkQuote(id, 1, "actor");
        Assert.AreEqual("Work quote not found.", result.ErrorMessage); Assert.AreEqual(0, db.Saves);
    }

    [TestMethod]
    public void ConversionReadsFreshQuoteDespitePreviouslyTrackedValue()
    {
        using var db = Database(); var tracked = db.WorkQuote.Single(x => x.Id == 100);
        db.OnBegin = () =>
        {
            using var other = new SignupContext(db.Options);
            other.WorkQuote.Single(x => x.Id == 100).Labour = 321; other.SaveChanges();
        };
        var result = new VehicleInvoiceService(db).CreateFromWorkQuote(100, 1, "actor");
        Assert.IsTrue(result.Success); Assert.AreEqual(321m, result.Data.Labour);
        Assert.AreEqual(100m, tracked.Labour);
    }

    [TestMethod]
    public void MultipleQuotesOnOneInvoiceStillProduceSafeDuplicate()
    {
        using var db = Database();
        using (var other = new SignupContext(db.Options))
        {
            var invoice = VehicleInvoiceServiceTests.Invoice(); invoice.WorkQuoteId = 100;
            other.VehicleInvoice.Add(invoice);
            other.InvoiceWorkQuote.AddRange(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 100, GarageBusinessCustomerId = 1 },
                new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 101, GarageBusinessCustomerId = 1 });
            other.SaveChanges();
        }
        Assert.AreEqual("This work quote has already been converted to an invoice.",
            new VehicleInvoiceService(db).CreateFromWorkQuote(101, 1, "actor").ErrorMessage);
        Assert.AreEqual(0, db.Saves);
    }

    [DataTestMethod]
    [DataRow(1)] [DataRow(2)] [DataRow(3)]
    public void FailedSaveRollsBackDetachesAndClearsGeneratedState(int save)
    {
        using var db = Database(); db.FailSave = save;
        Assert.ThrowsException<DbUpdateException>(() => new VehicleInvoiceService(db).CreateFromWorkQuote(100, 1, "actor"));
        Assert.AreEqual(save, db.Saves); Assert.IsTrue(db.Events.Contains("rollback"));
        Assert.IsFalse(db.Events.Contains("commit")); Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
        Assert.AreEqual(0, db.Invoice!.Id); Assert.IsNull(db.Invoice.InvoiceNumber);
        if (db.Link != null) { Assert.AreEqual(0, db.Link.Id); Assert.IsNull(db.Link.InvoiceId); }
        // No row-count assertion: mock rollback does not undo InMemory writes.
    }

    [TestMethod]
    public void FailedCommitIsUncertainAndNeverRetried()
    {
        using var db = Database(); db.FailCommit = true;
        var error = Assert.ThrowsException<InvalidOperationException>(() => new VehicleInvoiceService(db).CreateFromWorkQuote(100, 1, "actor"));
        StringAssert.Contains(error.Message, "could not be confirmed");
        Assert.AreEqual(3, db.Saves); Assert.AreEqual(1, db.Events.Count(x => x == "commit"));
        Assert.AreEqual(1, db.Events.Count(x => x == "begin")); Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
    }

    private sealed class ObservedContext : SignupContext
    {
        private readonly Mock<DatabaseFacade> _facade;
        public DbContextOptions<SignupContext> Options { get; }
        public List<string> Events { get; } = new();
        public int Saves { get; private set; }
        public int FailSave { get; set; }
        public bool FailCommit { get; set; }
        public Action? OnBegin { get; set; }
        public VehicleInvoice? Invoice { get; private set; }
        public InvoiceWorkQuote? Link { get; private set; }
        public override DatabaseFacade Database => _facade?.Object ?? base.Database;
        public ObservedContext(DbContextOptions<SignupContext> options) : base(options)
        {
            Options = options;
            var transaction = new Mock<IDbContextTransaction>();
            transaction.Setup(x => x.Commit()).Callback(() => { Events.Add("commit"); if (FailCommit) throw new IOException("Lost acknowledgement"); });
            transaction.Setup(x => x.Rollback()).Callback(() => Events.Add("rollback"));
            transaction.Setup(x => x.Dispose()).Callback(() => Events.Add("dispose"));
            _facade = new Mock<DatabaseFacade>(this) { CallBase = true };
            _facade.Setup(x => x.BeginTransaction()).Returns(() => { Events.Add("begin"); OnBegin?.Invoke(); return transaction.Object; });
        }
        public override int SaveChanges()
        {
            Saves++; Events.Add("save" + Saves);
            Invoice ??= ChangeTracker.Entries<VehicleInvoice>().Single().Entity;
            Link ??= ChangeTracker.Entries<InvoiceWorkQuote>().SingleOrDefault()?.Entity;
            if (Saves == FailSave) throw new DbUpdateException("Injected failure");
            return base.SaveChanges();
        }
    }
}

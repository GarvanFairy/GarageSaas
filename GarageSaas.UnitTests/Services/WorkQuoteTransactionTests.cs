using GarageSaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// Orchestration/cleanup tests only: mock transactions cannot undo InMemory writes.
[TestClass]
public class WorkQuoteTransactionTests
{
    private static ObservedContext Database()
    {
        var options = VehicleInvoiceServiceTests.Options();
        using (var seed = new SignupContext(options)) VehicleInvoiceServiceTests.Seed(seed);
        return new ObservedContext(options);
    }
    [TestMethod]
    public void BothSavesAndResponsePreparationPrecedeCommitAndPublication()
    {
        using var db = Database(); var input = WorkQuoteTenantValidationTests.Input(); input.WorkItemIds.Add(1001); input.InvoiceNumber = "Q-LEGACY";
        db.OnCommit = () => { Assert.AreEqual(0, input.WorkQuoteId); Assert.AreEqual(0, input.Id); };
        var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "actor");
        Assert.IsTrue(result.Success, result.ErrorMessage); Assert.AreSame(input, result.Data);
        CollectionAssert.AreEqual(new[] { "begin", "save1", "save2", "prepare", "commit", "dispose" }, db.Events);
        Assert.IsTrue(input.WorkQuoteId > 0); Assert.IsTrue(input.Id > 0); Assert.AreEqual("Q-LEGACY", result.Data.InvoiceNumber);
        Assert.AreEqual(2, db.WorkQuoteWorkItem.Count(x => x.WorkQuoteId == input.WorkQuoteId));
    }

    [DataTestMethod]
    [DataRow(1, false)] [DataRow(1, true)] [DataRow(2, false)] [DataRow(2, true)]
    public void SaveFailuresRollbackDetachAndNeverPublishGeneratedIds(int save, bool after)
    {
        using var db = Database(); db.FailAt = save; db.FailAfter = after;
        var prior = db.WorkQuote.Single(x => x.Id == 100); var input = WorkQuoteTenantValidationTests.Input();
        Assert.ThrowsException<DbUpdateException>(() => new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "actor"));
        Assert.AreEqual(save, db.Saves); Assert.IsTrue(db.Events.Contains("rollback")); Assert.IsFalse(db.Events.Contains("commit"));
        Assert.AreEqual(0, input.WorkQuoteId); Assert.AreEqual(0, input.Id); Assert.AreEqual(100, prior.Id);
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count()); Assert.AreEqual(0, db.CreatedQuote!.Id);
        foreach (var link in db.CreatedLinks) { Assert.AreEqual(0, link.Id); Assert.IsNull(link.WorkQuoteId); }
        // Deliberately no persisted-row assertion: requires real SQL rollback.
    }

    [TestMethod]
    public void ResponsePreparationFailureRollsBackAndLeavesRequestUnchanged()
    {
        using var db = Database(); db.FailResponse = true; var input = WorkQuoteTenantValidationTests.Input();
        Assert.ThrowsException<IOException>(() => new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "actor"));
        Assert.AreEqual(2, db.Saves); Assert.IsTrue(db.Events.Contains("prepare")); Assert.IsTrue(db.Events.Contains("rollback"));
        Assert.IsFalse(db.Events.Contains("commit")); Assert.AreEqual(0, input.WorkQuoteId); Assert.AreEqual(0, input.Id);
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count()); Assert.AreEqual(0, db.CreatedQuote!.Id);
    }

    [TestMethod]
    public void ValidationIsAuthoritativeInsideTransactionWithoutWrites()
    {
        using var db = Database(); db.OnBegin = () =>
        {
            using var other = new SignupContext(db.Options);
            other.GarageBusinessCustomer.Single(x => x.Id == 101).GarageBusinessId = 2; other.SaveChanges();
        };
        var input = WorkQuoteTenantValidationTests.Input(); var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "actor");
        Assert.IsFalse(result.Success); Assert.AreEqual("Customer could not be found for this garage.", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "begin", "rollback", "dispose" }, db.Events);
        Assert.AreEqual(0, db.Saves); Assert.AreEqual(0, input.WorkQuoteId); Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
    }

    [TestMethod]
    public void InvalidGarageIsCheckedAfterBegin()
    {
        using var db = Database(); var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(WorkQuoteTenantValidationTests.Input(), 999, "actor");
        Assert.AreEqual("Garage context is invalid.", result.ErrorMessage); Assert.AreEqual(0, db.Saves);
        CollectionAssert.AreEqual(new[] { "begin", "rollback", "dispose" }, db.Events);
    }

    [TestMethod]
    public void CommitAcknowledgementFailureIsUncertainWithoutRetryOrPublishedIdentity()
    {
        using var db = Database(); db.FailCommit = true; var input = WorkQuoteTenantValidationTests.Input();
        var error = Assert.ThrowsException<InvalidOperationException>(() => new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "actor"));
        StringAssert.Contains(error.Message, "could not be confirmed"); Assert.AreEqual(2, db.Saves);
        Assert.AreEqual(1, db.Events.Count(x => x == "begin")); Assert.AreEqual(1, db.Events.Count(x => x == "commit"));
        Assert.AreEqual(0, input.WorkQuoteId); Assert.AreEqual(0, input.Id); Assert.AreEqual(0, db.CreatedQuote!.Id);
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
    }

    [TestMethod]
    public void BeginFailurePropagatesWithoutSavingOrRetry()
    {
        using var db = Database(); db.FailBegin = true;
        Assert.ThrowsException<IOException>(() => new WorkQuoteService(db).AddOrUpdateWorkQuote(WorkQuoteTenantValidationTests.Input(), 1, "actor"));
        Assert.AreEqual(0, db.Saves); CollectionAssert.AreEqual(new[] { "begin" }, db.Events);
    }

    [TestMethod]
    public void ExistingUpdateAndDeleteUseSeparateTransactions()
    {
        var options = VehicleInvoiceServiceTests.Options();
        using (var seed = new SignupContext(options)) VehicleInvoiceServiceTests.Seed(seed);
        using var db = new ObservedContext(options); var service = new WorkQuoteService(db);
        var edit = WorkQuoteTenantValidationTests.Input(100); edit.Comment = "Updated";
        Assert.IsTrue(service.AddOrUpdateWorkQuote(edit, 1, "editor").Success);
        Assert.IsTrue(service.DeleteWorkQuote(100, 1).Success);
        Assert.AreEqual(2, db.Saves); Assert.AreEqual(2, db.Events.Count(x => x == "commit"));
    }

    private sealed class ObservedContext : SignupContext
    {
        private readonly Mock<DatabaseFacade> _facade;
        public DbContextOptions<SignupContext> Options { get; }
        public List<string> Events { get; } = new();
        public int Saves { get; private set; }
        public int FailAt { get; set; }
        public bool FailAfter { get; set; }
        public bool FailResponse { get; set; }
        public bool FailCommit { get; set; }
        public bool FailBegin { get; set; }
        public Action? OnBegin { get; set; }
        public Action? OnCommit { get; set; }
        public WorkQuote? CreatedQuote { get; private set; }
        public List<WorkQuoteWorkItem> CreatedLinks { get; } = new();
        public override DatabaseFacade Database => _facade?.Object ?? base.Database;
        public override DbSet<WorkQuoteWorkItem> WorkQuoteWorkItem
        {
            get { if (Saves == 2) { Events.Add("prepare"); if (FailResponse) throw new IOException("Injected response preparation failure"); } return base.WorkQuoteWorkItem; }
            set => base.WorkQuoteWorkItem = value;
        }
        public ObservedContext(DbContextOptions<SignupContext> options) : base(options)
        {
            Options = options; var transaction = new Mock<IDbContextTransaction>();
            transaction.Setup(x => x.Commit()).Callback(() => { Events.Add("commit"); OnCommit?.Invoke(); if (FailCommit) throw new IOException("Lost acknowledgement"); });
            transaction.Setup(x => x.Rollback()).Callback(() => Events.Add("rollback"));
            transaction.Setup(x => x.Dispose()).Callback(() => Events.Add("dispose"));
            _facade = new Mock<DatabaseFacade>(this) { CallBase = true };
            _facade.Setup(x => x.BeginTransaction()).Returns(() =>
            { Events.Add("begin"); if (FailBegin) throw new IOException("Begin failed"); OnBegin?.Invoke(); return transaction.Object; });
        }
        public override int SaveChanges()
        {
            Saves++; Events.Add("save" + Saves);
            CreatedQuote ??= ChangeTracker.Entries<WorkQuote>().FirstOrDefault(x => x.State == EntityState.Added)?.Entity;
            CreatedLinks.AddRange(ChangeTracker.Entries<WorkQuoteWorkItem>().Where(x => x.State == EntityState.Added).Select(x => x.Entity));
            if (FailAt == Saves && !FailAfter) throw new DbUpdateException("Injected pre-save failure");
            var count = base.SaveChanges();
            if (FailAt == Saves && FailAfter) throw new DbUpdateException("Injected post-save failure");
            return count;
        }
    }
}

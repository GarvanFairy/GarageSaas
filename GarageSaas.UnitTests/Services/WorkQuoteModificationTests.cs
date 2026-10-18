using GarageSaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// Functional/orchestration coverage only; real rollback and locking need SQL.
[TestClass]
public class WorkQuoteModificationTests
{
    private static ObservedContext Database()
    {
        var options = VehicleInvoiceServiceTests.Options();
        using (var seed = new SignupContext(options)) VehicleInvoiceServiceTests.Seed(seed);
        return new ObservedContext(options);
    }

    [DataTestMethod]
    [DataRow(false, false)] [DataRow(false, true)] [DataRow(true, false)] [DataRow(true, true)]
    public void SaveFailureRollsBackAndDetachesOnlyTarget(bool delete, bool after)
    {
        using var db = Database(); var unrelated = db.WorkQuote.Single(x => x.Id == 101);
        db.FailSave = true; db.FailAfter = after;
        Assert.ThrowsException<DbUpdateException>(() => Modify(db, delete));
        Assert.AreEqual(1, db.Rollbacks); Assert.AreEqual(0, db.Commits);
        Assert.IsFalse(db.ChangeTracker.Entries<WorkQuote>().Any(x => x.Entity.Id == 100));
        Assert.IsFalse(db.ChangeTracker.Entries<WorkQuoteWorkItem>().Any());
        Assert.AreEqual(EntityState.Unchanged, db.Entry(unrelated).State);
        foreach (var link in db.NewLinks) { Assert.AreEqual(0, link.Id); Assert.IsNull(link.WorkQuoteId); }
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void CommitUncertaintyDoesNotRetry(bool delete)
    {
        using var db = Database(); db.FailCommit = true;
        var error = Assert.ThrowsException<InvalidOperationException>(() => Modify(db, delete));
        StringAssert.Contains(error.Message, "could not be confirmed");
        Assert.AreEqual(1, db.Begins); Assert.AreEqual(1, db.Commits); Assert.AreEqual(1, db.Saves);
        Assert.IsFalse(db.ChangeTracker.HasChanges());
    }

    [TestMethod]
    public void StaleUnchangedQuoteAndLinksCannotOverwriteFreshState()
    {
        using var db = Database(); var quote = db.WorkQuote.Single(x => x.Id == 100);
        var link = db.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == 100);
        using (var other = new SignupContext(db.Options))
        {
            other.WorkQuote.Single(x => x.Id == 100).CreatedBy = "fresh creator";
            other.WorkQuoteWorkItem.Remove(other.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == 100));
            other.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem { WorkQuoteId = 100, WorkItemId = 1001, GarageBusinessCustomerId = 1 });
            other.SaveChanges();
        }
        var input = WorkQuoteTenantValidationTests.Input(100); input.WorkItemIds.Add(1001);
        Assert.IsTrue(new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "editor").Success);
        Assert.AreEqual(EntityState.Detached, db.Entry(quote).State); Assert.AreEqual(EntityState.Detached, db.Entry(link).State);
        Assert.AreEqual("fresh creator", db.WorkQuote.AsNoTracking().Single(x => x.Id == 100).CreatedBy);
        CollectionAssert.AreEquivalent(new[] { 1000, 1001 }, db.WorkQuoteWorkItem.AsNoTracking().Where(x => x.WorkQuoteId == 100).Select(x => x.WorkItemId!.Value).ToArray());
    }

    [DataTestMethod]
    [DataRow(false, "quote")] [DataRow(true, "quote")] [DataRow(false, "link")] [DataRow(true, "link")]
    [DataRow(false, "unrelated")] [DataRow(true, "unrelated")]
    public void PendingEditsAreRejectedWithoutDiscardingThem(bool delete, string kind)
    {
        using var db = Database();
        if (kind == "quote") db.WorkQuote.Single(x => x.Id == 100).Comment = "pending";
        if (kind == "link") db.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == 100).UpdatedBy = "pending";
        if (kind == "unrelated") db.GarageBusiness.Single(x => x.Id == 1).GarageBusinessName = "pending";
        var result = delete ? new WorkQuoteService(db).DeleteWorkQuote(100, 1) :
            new WorkQuoteService(db).AddOrUpdateWorkQuote(WorkQuoteTenantValidationTests.Input(100), 1, "editor");
        Assert.IsFalse(result.Success); StringAssert.Contains(result.ErrorMessage, "Pending changes");
        Assert.IsTrue(db.ChangeTracker.HasChanges()); Assert.AreEqual(0, db.Begins); Assert.AreEqual(0, db.Saves);
    }

    [TestMethod]
    public void RejectedReplacementNeverChangesAuditOrLinks()
    {
        using var db = Database(); var input = WorkQuoteTenantValidationTests.Input(100); input.WorkItemIds = new() { 2000 };
        Assert.IsFalse(new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "attacker").Success);
        Assert.AreEqual(0, db.Saves); Assert.AreEqual(1, db.Rollbacks); Assert.AreEqual(0, db.Commits);
        Assert.IsNull(db.WorkQuote.AsNoTracking().Single(x => x.Id == 100).UpdatedBy);
        Assert.AreEqual(1000, db.WorkQuoteWorkItem.AsNoTracking().Single(x => x.WorkQuoteId == 100).WorkItemId);
    }

    private static void Modify(ObservedContext db, bool delete)
    {
        if (delete) new WorkQuoteService(db).DeleteWorkQuote(100, 1);
        else { var input = WorkQuoteTenantValidationTests.Input(100); input.WorkItemIds = new() { 1001 };
            new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "editor"); }
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void HistoricalGarageOwnedAndLegacyUpdateInputsRemainSupported(bool legacy)
    {
        using var db = Database();
        db.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem { WorkQuoteId = 101, GarageBusinessCustomerId = 1 });
        db.SaveChanges(); db.Saves = 0;
        var input = WorkQuoteTenantValidationTests.Input(101); input.VehicleId = 11;
        if (legacy) { input.WorkItemIds.Clear(); input.WorkItemId = 1001; }
        else input.WorkItemIds.Add(1001);
        var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "editor");
        Assert.IsTrue(result.Success, result.ErrorMessage); Assert.AreSame(input, result.Data);
        var read = new WorkQuoteService(db).GetWorkQuote(101, 1);
        Assert.IsTrue(read.Success); Assert.AreEqual(legacy ? 1 : 2, read.Data.WorkItems.Count);
        Assert.IsTrue(db.WorkQuoteWorkItem.AsNoTracking().Any(x => x.WorkQuoteId == 101 && x.WorkItemId == null));
        Assert.AreEqual(1, db.Saves); Assert.AreEqual(1, db.Commits);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void FreshMalformedGraphIsRejectedDespiteStaleTrackedEntries(bool delete)
    {
        using var db = Database(); _ = db.WorkQuote.Single(x => x.Id == 100);
        _ = db.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == 100);
        using (var other = new SignupContext(db.Options))
        {
            other.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == 100).WorkItemId = 2000;
            other.SaveChanges();
        }
        var result = delete ? new WorkQuoteService(db).DeleteWorkQuote(100, 1) :
            new WorkQuoteService(db).AddOrUpdateWorkQuote(WorkQuoteTenantValidationTests.Input(100), 1, "editor");
        Assert.IsFalse(result.Success); Assert.AreEqual(0, db.Saves); Assert.AreEqual(1, db.Rollbacks);
        Assert.IsNull(db.WorkQuote.AsNoTracking().Single(x => x.Id == 100).UpdatedBy);
        Assert.AreEqual(2000, db.WorkQuoteWorkItem.AsNoTracking().Single(x => x.WorkQuoteId == 100).WorkItemId);
    }

    private sealed class ObservedContext : SignupContext
    {
        private readonly Mock<DatabaseFacade> _facade;
        public DbContextOptions<SignupContext> Options { get; }
        public int Saves, Begins, Commits, Rollbacks;
        public bool FailSave, FailAfter, FailCommit;
        public List<WorkQuoteWorkItem> NewLinks { get; } = new();
        public override DatabaseFacade Database => _facade?.Object ?? base.Database;
        public ObservedContext(DbContextOptions<SignupContext> options) : base(options)
        {
            Options = options;
            var transaction = new Mock<IDbContextTransaction>();
            transaction.Setup(x => x.Commit()).Callback(() => { Commits++; if (FailCommit) throw new IOException("Lost acknowledgement"); });
            transaction.Setup(x => x.Rollback()).Callback(() => Rollbacks++);
            _facade = new Mock<DatabaseFacade>(this) { CallBase = true };
            _facade.Setup(x => x.BeginTransaction()).Returns(() => { Begins++; return transaction.Object; });
        }
        public override int SaveChanges()
        {
            Saves++; NewLinks.AddRange(ChangeTracker.Entries<WorkQuoteWorkItem>().Where(x => x.State == EntityState.Added).Select(x => x.Entity));
            if (FailSave && !FailAfter) throw new DbUpdateException("Before save");
            var result = base.SaveChanges();
            if (FailSave && FailAfter) throw new DbUpdateException("After save");
            return result;
        }
    }
}

using System.Data.Common;
using GarageSaas.Services;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// PREPARED ONLY: explicit approval required before enabling this database fixture.
[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlQuoteAtomicity")]
public class WorkQuoteSqlTransactionTests
{
    private static DisposableSqlDatabase? _database;
    private static SqlInvoiceGraph _graph = null!;
    private static int _secondItem;
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_QUOTE_ATOMIC_SQL_TESTS", "QuoteAtomic");
        _database.Create();
        using var db = new SignupContext(Options()); _graph = SqlInvoiceGraph.Seed(db);
        var item = new WorkItem { GarageBusinessCustomerId = _graph.GarageId, CreatedDate = DateTime.UtcNow,
            CreatedBy = "test", RepairInstructions = "Nullable historical relationships" };
        db.WorkItem.Add(item); db.SaveChanges(); _secondItem = item.Id;
    }
    [ClassCleanup]
    public static async Task Cleanup() { if (_database != null) await _database.DeleteAsync(); }
    [TestCleanup]
    public async Task DrainWorkers() { if (_database != null) await _database.DrainAsync(); }
    [TestInitialize]
    public async Task WaitForPreviousWorkers() { if (_database != null) await _database.DrainAsync(); }
    private static DbContextOptions<SignupContext> Options(DbCommandInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<SignupContext>(_database!.Options());
        if (interceptor != null) builder.AddInterceptors(interceptor); return builder.Options;
    }
    private static CombinedWorkQuoteWorkitem Input() => new()
    {
        CustomerId = _graph.CustomerId, VehicleId = _graph.VehicleId,
        WorkItemIds = new() { _graph.WorkItemId, _secondItem }, WorkQuoteDate = DateTime.Today,
        Comment = Guid.NewGuid().ToString("N"), InvoiceNumber = "Q-LEGACY-REFERENCE", Labour = 50
    };

    [TestMethod]
    public void BothSavesAndResponseCommitTogetherWithExistingIdentifiers()
    {
        using var db = new FaultContext(Options()); var input = Input();
        var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(input, _graph.GarageId, "actor");
        Assert.IsTrue(result.Success, result.ErrorMessage); Assert.AreSame(input, result.Data);
        Assert.AreEqual(2, db.Saves); Assert.AreEqual(1, db.Transactions.Distinct().Count());
        Assert.IsFalse(db.Transactions.Contains(Guid.Empty)); Assert.IsTrue(input.WorkQuoteId > 0); Assert.IsTrue(input.Id > 0);
        using var check = new SignupContext(Options());
        Assert.AreEqual(input.InvoiceNumber, check.WorkQuote.Single(x => x.Id == input.WorkQuoteId).InvoiceNumber);
        var links = check.WorkQuoteWorkItem.AsNoTracking().Where(x => x.WorkQuoteId == input.WorkQuoteId).ToList();
        Assert.AreEqual(2, links.Count); Assert.IsTrue(links.All(x => x.GarageBusinessCustomerId == _graph.GarageId && x.CreatedBy == "actor"));
        CollectionAssert.AreEquivalent(input.WorkItemIds, links.Select(x => x.WorkItemId!.Value).ToList());
        Assert.IsTrue(links.Any(x => x.Id == input.Id)); Assert.IsNull(db.Database.CurrentTransaction);
    }

    [DataTestMethod]
    [DataRow(1, false)] [DataRow(1, true)] [DataRow(2, false)] [DataRow(2, true)]
    public void FailureAroundEitherSaveLeavesNoQuoteOrLinks(int save, bool after)
    {
        using var db = new FaultContext(Options()) { FailAt = save, FailAfter = after }; var input = Input();
        using var initial = new SignupContext(Options()); var quotes = initial.WorkQuote.Count(); var links = initial.WorkQuoteWorkItem.Count();
        Assert.ThrowsException<DbUpdateException>(() => new WorkQuoteService(db).AddOrUpdateWorkQuote(input, _graph.GarageId, "actor"));
        AssertRollback(db, input, quotes, links);
    }

    [TestMethod]
    public void ResponsePreparationFailureRollsBackBothSaves()
    {
        using var db = new FaultContext(Options()) { FailResponse = true }; var input = Input();
        using var initial = new SignupContext(Options()); var quotes = initial.WorkQuote.Count(); var links = initial.WorkQuoteWorkItem.Count();
        Assert.ThrowsException<IOException>(() => new WorkQuoteService(db).AddOrUpdateWorkQuote(input, _graph.GarageId, "actor"));
        Assert.AreEqual(2, db.Saves); AssertRollback(db, input, quotes, links);
    }

    [TestMethod]
    public void InvalidReferencesPerformNoWrites()
    {
        using var db = new FaultContext(Options()); var input = Input(); input.CustomerId = int.MaxValue;
        using var initial = new SignupContext(Options()); var quotes = initial.WorkQuote.Count(); var links = initial.WorkQuoteWorkItem.Count();
        var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(input, _graph.GarageId, "actor");
        Assert.IsFalse(result.Success); Assert.AreEqual(0, db.Saves); AssertRollback(db, input, quotes, links);
    }

    private static void AssertRollback(FaultContext db, CombinedWorkQuoteWorkitem input, int quotes, int links)
    {
        Assert.AreEqual(0, input.WorkQuoteId); Assert.AreEqual(0, input.Id);
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count()); Assert.IsNull(db.Database.CurrentTransaction);
        if (db.CreatedQuote != null) Assert.AreEqual(0, db.CreatedQuote.Id);
        foreach (var link in db.CreatedLinks) { Assert.AreEqual(0, link.Id); Assert.IsNull(link.WorkQuoteId); }
        using var check = new SignupContext(Options());
        Assert.AreEqual(quotes, check.WorkQuote.Count()); Assert.AreEqual(links, check.WorkQuoteWorkItem.Count());
        Assert.IsFalse(check.WorkQuote.Any(x => x.Comment == input.Comment));
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public async Task ConversionWaitsForUncommittedCreationThenSeesCompleteGraphOrNoQuote(bool rollBack)
    {
        var input = Input(); using var saved = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        using var attempted = new LockAttempt(); int quoteId = 0;
        var first = _database!.Track(Task.Run(() =>
        {
            using var db = new FaultContext(Options());
            db.AfterFirstSave = () =>
            {
                quoteId = db.CreatedQuote!.Id; saved.Set();
                if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Creation gate timed out");
                if (rollBack) throw new DbUpdateException("Injected rollback after first save");
            };
            return new WorkQuoteService(db).AddOrUpdateWorkQuote(input, _graph.GarageId, "creator");
        }));
        Task<GarageSaas.Services.Models.ServiceResult<VehicleInvoice>>? conversion = null;
        try
        {
            Assert.IsTrue(saved.Wait(TimeSpan.FromSeconds(10))); Assert.IsTrue(quoteId > 0);
            Assert.AreEqual(0, input.WorkQuoteId, "Uncommitted identity must not be published.");
            conversion = _database.Track(Task.Run(() =>
            {
                using var db = new SignupContext(Options(attempted));
                return new VehicleInvoiceService(db).CreateFromWorkQuote(quoteId, _graph.GarageId, "converter");
            }));
            Assert.IsTrue(attempted.Started.Wait(TimeSpan.FromSeconds(5))); await Task.Delay(300);
            Assert.IsFalse(conversion.IsCompleted, "Conversion observed an uncommitted quote.");
        }
        finally { release.Set(); await _database.DrainAsync(); }
        if (rollBack) await Assert.ThrowsExceptionAsync<DbUpdateException>(async () => { await first.WaitAsync(TimeSpan.FromSeconds(10)); });
        else Assert.IsTrue((await first.WaitAsync(TimeSpan.FromSeconds(10))).Success);
        Assert.IsNotNull(conversion); var result = await conversion!.WaitAsync(TimeSpan.FromSeconds(10));
        using var check = new SignupContext(Options());
        if (rollBack)
        {
            Assert.AreEqual("Work quote not found.", result.ErrorMessage);
            Assert.IsFalse(check.WorkQuote.Any(x => x.Id == quoteId)); Assert.IsFalse(check.WorkQuoteWorkItem.Any(x => x.WorkQuoteId == quoteId));
            Assert.IsFalse(check.VehicleInvoice.Any(x => x.WorkQuoteId == quoteId));
        }
        else
        {
            Assert.IsTrue(result.Success, result.ErrorMessage); Assert.AreEqual(2, check.WorkQuoteWorkItem.Count(x => x.WorkQuoteId == quoteId));
            Assert.AreEqual(result.Data.Id, check.InvoiceWorkQuote.Single(x => x.WorkQuoteId == quoteId).InvoiceId);
            Assert.AreEqual(2, new VehicleInvoiceService(check).GetVehicleInvoice(result.Data.Id, _graph.GarageId).Data.WorkItems.Count);
        }
    }

    [TestMethod]
    public void LostAcknowledgementAfterActualCommitDoesNotRetryOrClaimRollback()
    {
        using var db = new CommitLossContext(Options()); var input = Input();
        var error = Assert.ThrowsException<InvalidOperationException>(() => new WorkQuoteService(db).AddOrUpdateWorkQuote(input, _graph.GarageId, "actor"));
        StringAssert.Contains(error.Message, "could not be confirmed"); Assert.AreEqual(1, db.Commits); Assert.AreEqual(2, db.Saves);
        Assert.AreEqual(0, input.WorkQuoteId); Assert.AreEqual(0, input.Id); Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
        using var check = new SignupContext(Options()); var quote = check.WorkQuote.Single(x => x.Comment == input.Comment);
        Assert.AreEqual(2, check.WorkQuoteWorkItem.Count(x => x.WorkQuoteId == quote.Id));
    }

    private class FaultContext(DbContextOptions<SignupContext> options) : SignupContext(options)
    {
        public int Saves { get; private set; }
        public int FailAt { get; set; }
        public bool FailAfter { get; set; }
        public bool FailResponse { get; set; }
        public Action? AfterFirstSave { get; set; }
        public WorkQuote? CreatedQuote { get; private set; }
        public List<WorkQuoteWorkItem> CreatedLinks { get; } = new();
        public List<Guid> Transactions { get; } = new();
        public override DbSet<WorkQuoteWorkItem> WorkQuoteWorkItem
        {
            get { if (FailResponse && Saves == 2) throw new IOException("Injected response preparation failure"); return base.WorkQuoteWorkItem; }
            set => base.WorkQuoteWorkItem = value;
        }
        public override int SaveChanges()
        {
            Saves++; Transactions.Add(Database.CurrentTransaction?.TransactionId ?? Guid.Empty);
            CreatedQuote ??= ChangeTracker.Entries<WorkQuote>().Single(x => x.State == EntityState.Added).Entity;
            CreatedLinks.AddRange(ChangeTracker.Entries<WorkQuoteWorkItem>().Where(x => x.State == EntityState.Added).Select(x => x.Entity));
            if (FailAt == Saves && !FailAfter) throw new DbUpdateException("Injected pre-save failure");
            var count = base.SaveChanges();
            if (Saves == 1) AfterFirstSave?.Invoke();
            if (FailAt == Saves && FailAfter) throw new DbUpdateException("Injected post-save failure");
            return count;
        }
    }
    private sealed class CommitLossContext : FaultContext
    {
        private readonly Mock<DatabaseFacade> _facade;
        public int Commits { get; private set; }
        public override DatabaseFacade Database => _facade?.Object ?? base.Database;
        public CommitLossContext(DbContextOptions<SignupContext> options) : base(options)
        {
            _facade = new Mock<DatabaseFacade>(this) { CallBase = true };
            _facade.Setup(x => x.BeginTransaction()).Returns(() =>
            {
                var real = base.Database.BeginTransaction(); var transaction = new Mock<IDbContextTransaction>();
                transaction.Setup(x => x.Commit()).Callback(() => { Commits++; real.Commit(); throw new IOException("Injected lost acknowledgement AFTER commit"); });
                transaction.Setup(x => x.Rollback()).Callback(() => real.Rollback());
                transaction.Setup(x => x.Dispose()).Callback(() => real.Dispose());
                return transaction.Object;
            });
        }
    }
    private sealed class LockAttempt : DbCommandInterceptor, IDisposable
    {
        public ManualResetEventSlim Started { get; } = new();
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            // The D3a customer-classification read precedes the quote update lock
            // and must also wait behind the first uncommitted quote insertion.
            if (command.CommandText.Contains("[WorkQuote]", StringComparison.Ordinal)) Started.Set();
            return result;
        }
        public void Dispose() => Started.Dispose();
    }
}

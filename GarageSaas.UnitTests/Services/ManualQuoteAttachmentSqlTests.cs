using System.Data.Common;
using GarageSaas.Services;
using GarageSaas.Services.Models;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// Prepared only; separate execution approval required. Existing InvoiceAtomic
// GUID namespace and unchanged guards, with a separate fixture opt-in.
[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlManualQuoteAttachment")]
public class ManualQuoteAttachmentSqlTests
{
    private static DisposableSqlDatabase? _database;
    private static SqlInvoiceGraph _graph = null!;
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_MANUAL_QUOTE_ATTACHMENT_SQL_TESTS", "InvoiceAtomic");
        _database.Create(); using var db = new SignupContext(_database.Options()); _graph = SqlInvoiceGraph.Seed(db);
    }
    [ClassCleanup]
    public static async Task Cleanup() { if (_database != null) await _database.DeleteAsync(); }
    [TestCleanup]
    public async Task Drain() { if (_database != null) await _database.DrainAsync(); }
    private static DbContextOptions<SignupContext> Options(DbCommandInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<SignupContext>(_database!.Options());
        if (interceptor != null) builder.AddInterceptors(interceptor); return builder.Options;
    }
    private static int Quote()
    {
        using var db = new SignupContext(Options());
        var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(new CombinedWorkQuoteWorkitem
        { CustomerId = _graph.CustomerId, VehicleId = _graph.VehicleId, WorkItemId = _graph.WorkItemId, WorkQuoteDate = DateTime.Today }, _graph.GarageId, "seed");
        Assert.IsTrue(result.Success, result.ErrorMessage); return result.Data.WorkQuoteId;
    }
    private static VehicleInvoice Input(int quoteId) => new()
    {
        WorkQuoteId = quoteId, CustomerId = _graph.CustomerId, GarageBusinessCustomerId = _graph.CustomerId,
        VehicleId = _graph.VehicleId, InvoiceDate = new DateTime(2026, 1, 1), InvoiceStatus = "Pending"
    };
    private static ServiceResult<VehicleInvoice> Create(SignupContext db, int quoteId, bool manual) => manual
        ? new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(Input(quoteId), _graph.GarageId, "manual")
        : new VehicleInvoiceService(db).CreateFromWorkQuote(quoteId, _graph.GarageId, "convert");

    [DataTestMethod]
    [DataRow(true, true, false)] [DataRow(true, false, false)] [DataRow(false, true, false)]
    [DataRow(true, true, true)]
    public async Task SameQuoteSerializesAndDifferentQuotesProceed(bool firstManual, bool secondManual, bool different)
    {
        var firstId = Quote(); var secondId = different ? Quote() : firstId;
        using var acquired = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        using var firstLock = new LockObserver { AfterLock = () =>
            { acquired.Set(); if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Quote lock gate"); } };
        using var secondLock = new LockObserver();
        var first = _database!.Track(Task.Run(() => { using var db = new SignupContext(Options(firstLock)); return Create(db, firstId, firstManual); }));
        Task<ServiceResult<VehicleInvoice>>? second = null;
        try
        {
            Assert.IsTrue(acquired.Wait(TimeSpan.FromSeconds(10)));
            second = _database.Track(Task.Run(() => { using var db = new SignupContext(Options(secondLock)); return Create(db, secondId, secondManual); }));
            Assert.IsTrue(secondLock.Started.Wait(TimeSpan.FromSeconds(5)));
            if (different) Assert.IsTrue((await second.WaitAsync(TimeSpan.FromSeconds(5))).Success);
            else { await Task.Delay(300); Assert.IsFalse(second.IsCompleted, "Same-quote contender bypassed lock"); }
        }
        finally { release.Set(); await _database.DrainAsync(); }
        var winner = await first.WaitAsync(TimeSpan.FromSeconds(10)); Assert.IsTrue(winner.Success, winner.ErrorMessage);
        Assert.IsNotNull(second); var contender = await second.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(different, contender.Success);
        if (!different) Assert.AreEqual(secondManual ? "This work quote is already linked to an invoice." :
            "This work quote has already been converted to an invoice.", contender.ErrorMessage);
        using var check = new SignupContext(Options());
        Assert.AreEqual(1, check.VehicleInvoice.Count(x => x.WorkQuoteId == firstId));
        Assert.AreEqual(firstManual ? 0 : 1, check.InvoiceWorkQuote.Count(x => x.WorkQuoteId == firstId));
        StringAssert.StartsWith(winner.Data.InvoiceNumber, "INV-");
        if (different) Assert.AreEqual(1, check.VehicleInvoice.Count(x => x.WorkQuoteId == secondId));
    }

    [DataTestMethod]
    [DataRow(1, false)] [DataRow(1, true)] [DataRow(2, false)] [DataRow(2, true)]
    public void SaveFailureRollsBackManualAttachmentAndAllowsLaterConversion(int save, bool after)
    {
        var id = Quote(); using var db = new FaultContext(Options()) { FailAt = save, FailAfter = after };
        var result = Create(db, id, true); Assert.IsFalse(result.Success); Assert.IsNull(result.Data);
        Assert.AreEqual(save, db.Saves); Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
        db.FailAt = 0; db.SaveChanges();
        using var check = new SignupContext(Options());
        Assert.IsFalse(check.VehicleInvoice.Any(x => x.WorkQuoteId == id));
        Assert.IsFalse(check.InvoiceWorkQuote.Any(x => x.WorkQuoteId == id));
        Assert.IsTrue(Create(check, id, false).Success);
    }

    [TestMethod]
    public void LostCommitAcknowledgementNeverRetriesOrCreatesAnotherInvoice()
    {
        var id = Quote(); using var db = new CommitLossContext(Options());
        var result = Create(db, id, true); Assert.IsFalse(result.Success); Assert.IsNull(result.Data);
        StringAssert.Contains(result.ErrorMessage, "could not be confirmed"); Assert.AreEqual(1, db.Commits);
        Assert.AreEqual(2, db.Saves); Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
        using var check = new SignupContext(Options());
        Assert.AreEqual(1, check.VehicleInvoice.Count(x => x.WorkQuoteId == id));
        Assert.IsNotNull(check.VehicleInvoice.Single(x => x.WorkQuoteId == id).InvoiceNumber);
        Assert.IsFalse(Create(check, id, false).Success);
    }

    private class FaultContext : SignupContext
    {
        public int Saves, FailAt; public bool FailAfter;
        public FaultContext(DbContextOptions<SignupContext> options) : base(options) { }
        public override int SaveChanges()
        {
            Saves++; if (Saves == FailAt && !FailAfter) throw new DbUpdateException("Before save");
            var result = base.SaveChanges(); if (Saves == FailAt && FailAfter) throw new DbUpdateException("After save"); return result;
        }
    }
    private sealed class CommitLossContext : FaultContext
    {
        private readonly Mock<DatabaseFacade> _facade; public int Commits;
        public override DatabaseFacade Database => _facade?.Object ?? base.Database;
        public CommitLossContext(DbContextOptions<SignupContext> options) : base(options)
        {
            _facade = new Mock<DatabaseFacade>(this) { CallBase = true };
            _facade.Setup(x => x.BeginTransaction()).Returns(() =>
            {
                var real = base.Database.BeginTransaction(); var wrapper = new Mock<IDbContextTransaction>();
                wrapper.Setup(x => x.Commit()).Callback(() => { Commits++; real.Commit(); throw new IOException("Lost acknowledgement after actual commit"); });
                wrapper.Setup(x => x.Rollback()).Callback(() => real.Rollback());
                wrapper.Setup(x => x.Dispose()).Callback(() => real.Dispose()); return wrapper.Object;
            });
        }
    }
    private sealed class LockObserver : DbCommandInterceptor, IDisposable
    {
        public ManualResetEventSlim Started { get; } = new(); public Action? AfterLock; private int _observed;
        private static bool IsLock(DbCommand command) => command.CommandText.Contains("UPDLOCK") && command.CommandText.Contains("HOLDLOCK") && command.CommandText.Contains("[WorkQuote]");
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        { if (IsLock(command)) Started.Set(); return result; }
        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        { if (IsLock(command) && Interlocked.Exchange(ref _observed, 1) == 0) AfterLock?.Invoke(); return result; }
        public void Dispose() => Started.Dispose();
    }
}

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

// Prepared only: separate execution approval required. No schema/guard changes.
[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlInvoiceUpdate")]
public class InvoiceUpdateSqlTests
{
    private static DisposableSqlDatabase? _database;
    private static SqlInvoiceGraph _garage = null!, _otherGarage = null!;
    public TestContext TestContext { get; set; } = null!;
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_INVOICE_UPDATE_SQL_TESTS", "InvoiceAtomic");
        _database.Create(); using var db = new SignupContext(Options());
        _garage = SqlInvoiceGraph.Seed(db); _otherGarage = SqlInvoiceGraph.Seed(db);
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
    private static int Quote(SqlInvoiceGraph garage)
    {
        using var db = new SignupContext(Options());
        var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(new CombinedWorkQuoteWorkitem
        { CustomerId = garage.CustomerId, VehicleId = garage.VehicleId, WorkItemId = garage.WorkItemId, WorkQuoteDate = DateTime.Today }, garage.GarageId, "seed");
        Assert.IsTrue(result.Success, result.ErrorMessage); return result.Data.WorkQuoteId;
    }
    private static VehicleInvoice Input(SqlInvoiceGraph garage, int id = 0, int? quote = null, string comment = "original") => new()
    {
        Id = id, CustomerId = garage.CustomerId, GarageBusinessCustomerId = garage.CustomerId, VehicleId = garage.VehicleId,
        WorkQuoteId = quote, InvoiceStatus = "Pending", InvoiceDate = new DateTime(2026, 1, 1), Paid = false,
        InvoiceNumber = id == 0 ? null : $"INV-2026-{id:D6}", Comment = comment
    };
    private static int Invoice(SqlInvoiceGraph garage)
    {
        using var db = new SignupContext(Options()); var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(Input(garage), garage.GarageId, "seed");
        Assert.IsTrue(result.Success, result.ErrorMessage); return result.Data.Id;
    }
    private static ServiceResult Operate(SignupContext db, string operation, int id, int quote, SqlInvoiceGraph garage)
    {
        var service = new VehicleInvoiceService(db);
        return operation == "convert" ? service.CreateFromWorkQuote(quote, garage.GarageId, "converter") :
            service.AddOrUpdateVehicleInvoice(Input(garage, operation == "create" ? 0 : id, quote, "attached"), garage.GarageId, "writer");
    }

    [DataTestMethod]
    [DataRow("update", "update")] [DataRow("update", "create")] [DataRow("create", "update")]
    [DataRow("update", "convert")] [DataRow("convert", "update")]
    public async Task SameQuoteCompetingWritersSerializeAndOnlyOneAttaches(string firstOperation, string secondOperation)
    {
        var quote = Quote(_garage); var firstInvoice = Invoice(_garage); var secondInvoice = Invoice(_garage);
        using var reached = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        using var firstObserver = new Observer { AfterQuoteLock = () => Gate(reached, release) };
        using var secondObserver = new Observer();
        var first = _database!.Track(Task.Run(() => { using var db = new SignupContext(Options(firstObserver)); return Operate(db, firstOperation, firstInvoice, quote, _garage); }));
        Task<ServiceResult>? second = null;
        try
        {
            Assert.IsTrue(reached.Wait(TimeSpan.FromSeconds(10)));
            second = _database.Track(Task.Run(() => { using var db = new SignupContext(Options(secondObserver)); return Operate(db, secondOperation, secondInvoice, quote, _garage); }));
            Assert.IsTrue(secondObserver.QuoteAttempt.Wait(TimeSpan.FromSeconds(5))); await Task.Delay(300);
            Assert.IsFalse(second.IsCompleted);
        }
        finally { release.Set(); await _database.DrainAsync(); }
        Assert.IsTrue((await first.WaitAsync(TimeSpan.FromSeconds(10))).Success);
        Assert.IsNotNull(second); Assert.IsFalse((await second.WaitAsync(TimeSpan.FromSeconds(10))).Success);
        using var check = new SignupContext(Options());
        Assert.AreEqual(1, check.VehicleInvoice.Count(x => x.WorkQuoteId == quote));
        Assert.AreEqual(firstOperation == "convert" ? 1 : 0, check.InvoiceWorkQuote.Count(x => x.WorkQuoteId == quote));
        Assert.IsNull(check.VehicleInvoice.Single(x => x.Id == secondInvoice).WorkQuoteId);
    }

    [TestMethod]
    public async Task StaleNullRequestAndTrackedInvoiceCannotOverwriteCommittedAttachment()
    {
        var id = Invoice(_garage); var quote = Quote(_garage);
        using var reached = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var stale = _database!.Track(Task.Run(() =>
        {
            using var db = new ControlledContext(Options()) { OnBegin = () => Gate(reached, release) };
            _ = db.VehicleInvoice.Single(x => x.Id == id); // stale unchanged tracked row
            return new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(Input(_garage, id), _garage.GarageId, "stale");
        }));
        try
        {
            Assert.IsTrue(reached.Wait(TimeSpan.FromSeconds(10)));
            using var db = new SignupContext(Options()); Assert.IsTrue(Operate(db, "update", id, quote, _garage).Success);
        }
        finally { release.Set(); await _database.DrainAsync(); }
        Assert.IsFalse((await stale.WaitAsync(TimeSpan.FromSeconds(10))).Success);
        using var check = new SignupContext(Options()); Assert.AreEqual(quote, check.VehicleInvoice.Single(x => x.Id == id).WorkQuoteId);
    }

    [TestMethod]
    public async Task ConcurrentScalarUpdatesToSameInvoiceSerializeWithoutQuoteLocks()
    {
        var id = Invoice(_garage); using var reached = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        using var observer = new Observer();
        var first = _database!.Track(Task.Run(() =>
        {
            using var db = new ControlledContext(Options()) { AfterSave = () => Gate(reached, release) };
            return new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(Input(_garage, id, comment: "first"), _garage.GarageId, "first");
        }));
        Task<ServiceResult<VehicleInvoice>>? second = null;
        try
        {
            Assert.IsTrue(reached.Wait(TimeSpan.FromSeconds(10)));
            second = _database.Track(Task.Run(() =>
            {
                using var db = new SignupContext(Options(observer));
                return new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(Input(_garage, id, comment: "second"), _garage.GarageId, "second");
            }));
            // Preliminary SELECT can itself wait on the first writer's X lock.
            Assert.IsTrue(observer.InvoiceRead.Wait(TimeSpan.FromSeconds(5))); await Task.Delay(300); Assert.IsFalse(second.IsCompleted);
        }
        finally { release.Set(); await _database.DrainAsync(); }
        Assert.IsTrue((await first.WaitAsync(TimeSpan.FromSeconds(10))).Success); Assert.IsNotNull(second);
        Assert.IsTrue((await second.WaitAsync(TimeSpan.FromSeconds(10))).Success); Assert.AreEqual(0, observer.QuoteLocks);
        using var check = new SignupContext(Options()); Assert.AreEqual("second", check.VehicleInvoice.Single(x => x.Id == id).Comment);
    }

    [DataTestMethod]
    [DataRow(false, false)] [DataRow(false, true)] [DataRow(true, true)]
    public async Task TwoGaragesWithInvoiceOrJoinWritesUnderway(bool firstConversion, bool secondAttachment)
    {
        var firstId = Invoice(_garage); var secondId = Invoice(_otherGarage);
        var firstQuote = Quote(_garage); var secondQuote = Quote(_otherGarage);
        using var reached = new ManualResetEventSlim(); using var release = new ManualResetEventSlim(); using var observer = new Observer();
        var first = _database!.Track(Task.Run(() =>
        {
            using var db = new ControlledContext(Options()) { GateSave = firstConversion ? 3 : 1, AfterSave = () => Gate(reached, release) };
            return Operate(db, firstConversion ? "convert" : "update", firstId, firstQuote, _garage);
        }));
        Task<ServiceResult<VehicleInvoice>>? second = null;
        try
        {
            Assert.IsTrue(reached.Wait(TimeSpan.FromSeconds(10)));
            second = _database.Track(Task.Run(() =>
            {
                using var db = new SignupContext(Options(observer));
                return new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(Input(_otherGarage, secondId, secondAttachment ? secondQuote : null), _otherGarage.GarageId, "other");
            }));
            Assert.IsTrue(observer.InvoiceRead.Wait(TimeSpan.FromSeconds(5)));
            if (!secondAttachment) Assert.IsTrue((await second.WaitAsync(TimeSpan.FromSeconds(5))).Success);
            else { await Task.Delay(300); TestContext.WriteLine($"Other garage completed while writes uncommitted: {second.IsCompleted}. Scans may legitimately block."); }
        }
        finally { release.Set(); await _database.DrainAsync(); }
        Assert.IsTrue((await first.WaitAsync(TimeSpan.FromSeconds(10))).Success); Assert.IsNotNull(second);
        Assert.IsTrue((await second.WaitAsync(TimeSpan.FromSeconds(10))).Success);
        using var check = new SignupContext(Options());
        var other = check.VehicleInvoice.Single(x => x.Id == secondId);
        Assert.AreEqual(_otherGarage.GarageId, other.GarageBusinessId); Assert.AreEqual(secondAttachment ? secondQuote : (int?)null, other.WorkQuoteId);
        if (!secondAttachment) Assert.AreEqual(0, observer.QuoteLocks);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void FailedSaveRollsBackValuesAttachmentAndLaterSave(bool after)
    {
        var id = Invoice(_garage); var quote = Quote(_garage);
        using var db = new ControlledContext(Options()) { FailSave = true, FailAfter = after };
        Assert.ThrowsException<DbUpdateException>(() => Operate(db, "update", id, quote, _garage));
        Assert.IsFalse(db.ChangeTracker.HasChanges()); db.FailSave = false; db.SaveChanges();
        using var check = new SignupContext(Options()); var invoice = check.VehicleInvoice.Single(x => x.Id == id);
        Assert.IsNull(invoice.WorkQuoteId); Assert.AreEqual("original", invoice.Comment); Assert.IsNull(invoice.UpdatedBy); Assert.IsNull(invoice.UpdatedDate);
    }

    [TestMethod]
    public void ActualCommitThenAcknowledgementLossNeverRetries()
    {
        var id = Invoice(_garage); var quote = Quote(_garage); using var db = new ControlledContext(Options()) { LoseCommit = true };
        var error = Assert.ThrowsException<InvalidOperationException>(() => Operate(db, "update", id, quote, _garage));
        StringAssert.Contains(error.Message, "could not be confirmed"); Assert.AreEqual(1, db.Commits); Assert.AreEqual(1, db.Saves);
        Assert.IsFalse(db.ChangeTracker.HasChanges()); using var check = new SignupContext(Options());
        Assert.AreEqual(1, check.VehicleInvoice.Count(x => x.WorkQuoteId == quote));
    }

    private static void Gate(ManualResetEventSlim reached, ManualResetEventSlim release)
    { reached.Set(); if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("SQL coordination gate"); }
    private sealed class ControlledContext : SignupContext
    {
        private readonly Mock<DatabaseFacade> _facade;
        public override DatabaseFacade Database => _facade?.Object ?? base.Database;
        public Action? OnBegin, AfterSave; public int GateSave = 1, Saves, Commits; public bool FailSave, FailAfter, LoseCommit;
        public ControlledContext(DbContextOptions<SignupContext> options) : base(options)
        {
            _facade = new Mock<DatabaseFacade>(this) { CallBase = true };
            _facade.Setup(x => x.BeginTransaction()).Returns(() =>
            {
                var real = base.Database.BeginTransaction();
                try { OnBegin?.Invoke(); } catch { real.Dispose(); throw; }
                var wrapper = new Mock<IDbContextTransaction>();
                wrapper.Setup(x => x.Commit()).Callback(() => { Commits++; real.Commit(); if (LoseCommit) throw new IOException("Lost actual commit acknowledgement"); });
                wrapper.Setup(x => x.Rollback()).Callback(() => real.Rollback()); wrapper.Setup(x => x.Dispose()).Callback(() => real.Dispose()); return wrapper.Object;
            });
        }
        public override int SaveChanges()
        {
            Saves++; if (FailSave && !FailAfter) throw new DbUpdateException("Before save");
            var count = base.SaveChanges(); if (FailSave && FailAfter) throw new DbUpdateException("After save");
            if (Saves == GateSave) AfterSave?.Invoke(); return count;
        }
    }
    private sealed class Observer : DbCommandInterceptor, IDisposable
    {
        public ManualResetEventSlim QuoteAttempt { get; } = new(); public ManualResetEventSlim InvoiceRead { get; } = new();
        public Action? AfterQuoteLock; public int QuoteLocks;
        private static bool QuoteLock(DbCommand command) => command.CommandText.Contains("[WorkQuote]") && command.CommandText.Contains("UPDLOCK");
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result)
        {
            if (command.CommandText.Contains("[VehicleInvoice]")) InvoiceRead.Set();
            if (QuoteLock(command)) { Interlocked.Increment(ref QuoteLocks); QuoteAttempt.Set(); } return result;
        }
        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData data, DbDataReader result)
        { if (QuoteLock(command)) AfterQuoteLock?.Invoke(); return result; }
        public void Dispose() { QuoteAttempt.Dispose(); InvoiceRead.Dispose(); }
    }
}

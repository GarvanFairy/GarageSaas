using GarageSaas.UnitTests.TestHelpers;
using System.Data.Common;
using GarageSaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// Prepared only. Requires explicit approval: creates/deletes an isolated database.
[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlQuoteConversion")]
public class QuoteConversionSqlTests
{
    private static DisposableSqlDatabase? _database;

    private static SqlInvoiceGraph _graph = null!;
    private static int _garage => _graph.GarageId;
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_QUOTE_CONVERSION_SQL_TESTS", "QuoteConversion");
        _database.Create();
        using var db = new SignupContext(Options());
        _graph = SqlInvoiceGraph.Seed(db);
        // Runtime mapping, not the outdated migration snapshot, controls the lock target.
        var entity = db.Model.FindEntityType(typeof(WorkQuote))!;
        Assert.AreEqual("WorkQuote", entity.GetTableName());
        Assert.AreEqual("Id", entity.FindPrimaryKey()!.Properties.Single().Name);
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
        if (interceptor != null) builder.AddInterceptors(interceptor);
        return builder.Options;
    }
    private static int Quote()
    {
        using var db = new SignupContext(Options());
        var quote = new WorkQuote { GarageBusinessCustomerId = _garage, CustomerId = _graph.CustomerId,
            VehicleId = _graph.VehicleId, CreatedDate = DateTime.UtcNow, CreatedBy = "test", Labour = 50 };
        db.WorkQuote.Add(quote); db.SaveChanges();
        db.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem { GarageBusinessCustomerId = _garage,
            WorkQuoteId = quote.Id, WorkItemId = _graph.WorkItemId, CreatedDate = DateTime.UtcNow, CreatedBy = "test" });
        db.SaveChanges(); return quote.Id;
    }

    [TestMethod]
    public async Task SameQuoteSecondConnectionWaitsThenReturnsDuplicate()
    {
        var quote = Quote(); using var gate = new LockGate(); using var secondGate = new LockGate();
        var first = _database!.Track(Task.Run(() => { using var db = new PausedContext(Options(), gate); return new VehicleInvoiceService(db).CreateFromWorkQuote(quote, _garage, "first"); }));
        Task<GarageSaas.Services.Models.ServiceResult<VehicleInvoice>>? second = null;
        try
        {
            Assert.IsTrue(gate.Acquired.Wait(TimeSpan.FromSeconds(10)), "First connection never acquired quote lock.");
            second = _database.Track(Task.Run(() => { using var db = new SignupContext(Options(secondGate)); return new VehicleInvoiceService(db).CreateFromWorkQuote(quote, _garage, "second"); }));
            Assert.IsTrue(secondGate.Attempted.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(300);
            Assert.IsFalse(second.IsCompleted, "Second connection completed while the first held the quote lock.");
        }
        finally { gate.Release.Set(); await _database.DrainAsync(); }
        Assert.IsTrue((await first.WaitAsync(TimeSpan.FromSeconds(15))).Success);
        Assert.IsNotNull(second);
        var duplicate = await second!.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.AreEqual("This work quote has already been converted to an invoice.", duplicate.ErrorMessage);
        using var observed = new SignupContext(Options());
        Assert.AreEqual(1, observed.VehicleInvoice.Count(x => x.WorkQuoteId == quote));
        Assert.AreEqual(1, observed.InvoiceWorkQuote.Count(x => x.WorkQuoteId == quote));
        var invoice = observed.VehicleInvoice.Single(x => x.WorkQuoteId == quote);
        var link = observed.InvoiceWorkQuote.Single(x => x.WorkQuoteId == quote);
        Assert.AreEqual(invoice.Id, link.InvoiceId); Assert.AreEqual(_garage, link.GarageBusinessCustomerId);
        Assert.AreEqual($"INV-{invoice.InvoiceDate:yyyy}-{invoice.Id:D6}", invoice.InvoiceNumber);
        Assert.AreEqual(_graph.CustomerId, invoice.CustomerId); Assert.AreEqual(_graph.VehicleId, invoice.VehicleId);
        Assert.IsNull(duplicate.Data);
    }

    [TestMethod]
    public async Task DifferentQuotesCanConvertWhileFirstQuoteLockIsHeld()
    {
        var quote = Quote(); var other = Quote(); using var gate = new LockGate();
        var first = _database!.Track(Task.Run(() => { using var db = new PausedContext(Options(), gate); return new VehicleInvoiceService(db).CreateFromWorkQuote(quote, _garage, "first"); }));
        try
        {
            Assert.IsTrue(gate.Acquired.Wait(TimeSpan.FromSeconds(10)));
            var second = _database.Track(Task.Run(() => { using var db = new SignupContext(Options()); return new VehicleInvoiceService(db).CreateFromWorkQuote(other, _garage, "second"); }));
            Assert.IsTrue((await second.WaitAsync(TimeSpan.FromSeconds(6))).Success);
        }
        finally { gate.Release.Set(); await _database.DrainAsync(); }
        Assert.IsTrue((await first.WaitAsync(TimeSpan.FromSeconds(15))).Success);
        using var check = new SignupContext(Options());
        foreach (var id in new[] { quote, other })
        {
            var invoice = check.VehicleInvoice.Single(x => x.WorkQuoteId == id);
            Assert.AreEqual(invoice.Id, check.InvoiceWorkQuote.Single(x => x.WorkQuoteId == id).InvoiceId);
            Assert.AreEqual($"INV-{invoice.InvoiceDate:yyyy}-{invoice.Id:D6}", invoice.InvoiceNumber);
        }
    }

    [DataTestMethod]
    [DataRow(1)] [DataRow(2)] [DataRow(3)]
    public void FailureAfterEachSaveRollsBackInvoiceNumberAndLink(int save)
    {
        var quote = Quote(); using var db = new FailingContext(Options(), save);
        using var initial = new SignupContext(Options());
        var invoicesBefore = initial.VehicleInvoice.Count(); var linksBefore = initial.InvoiceWorkQuote.Count();
        Assert.ThrowsException<DbUpdateException>(() => new VehicleInvoiceService(db).CreateFromWorkQuote(quote, _garage, "test"));
        using var observed = new SignupContext(Options());
        Assert.IsFalse(observed.VehicleInvoice.Any(x => x.WorkQuoteId == quote));
        Assert.IsFalse(observed.InvoiceWorkQuote.Any(x => x.WorkQuoteId == quote));
        Assert.AreEqual(invoicesBefore, observed.VehicleInvoice.Count()); Assert.AreEqual(linksBefore, observed.InvoiceWorkQuote.Count());
        Assert.IsTrue(observed.WorkQuote.Any(x => x.Id == quote));
        Assert.IsTrue(observed.WorkQuoteWorkItem.Any(x => x.WorkQuoteId == quote && x.WorkItemId == _graph.WorkItemId));
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count()); Assert.IsNull(db.Database.CurrentTransaction);
        Assert.AreEqual(0, db.CreatedInvoice!.Id); Assert.IsNull(db.CreatedInvoice.InvoiceNumber);
        if (db.CreatedLink != null) { Assert.AreEqual(0, db.CreatedLink.Id); Assert.IsNull(db.CreatedLink.InvoiceId); }
    }
    private sealed class FailingContext(DbContextOptions<SignupContext> options, int failSave) : SignupContext(options)
    {
        private int _saves;
        public VehicleInvoice? CreatedInvoice { get; private set; }
        public InvoiceWorkQuote? CreatedLink { get; private set; }
        public override int SaveChanges()
        {
            Assert.IsNotNull(Database.CurrentTransaction);
            CreatedInvoice ??= ChangeTracker.Entries<VehicleInvoice>().Single().Entity;
            CreatedLink ??= ChangeTracker.Entries<InvoiceWorkQuote>().SingleOrDefault()?.Entity;
            var count = base.SaveChanges();
            if (++_saves == failSave) throw new DbUpdateException("Injected post-save failure");
            return count;
        }
    }
    private sealed class PausedContext(DbContextOptions<SignupContext> options, LockGate gate) : SignupContext(options)
    {
        private bool _paused;
        public override int SaveChanges()
        {
            if (!_paused)
            {
                _paused = true;
                // Full lock query and validation have completed; no invoice write
                // has happened yet, isolating the quote-row lock in this test.
                gate.Acquired.Set();
                if (!gate.Release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Test lock release timed out.");
            }
            return base.SaveChanges();
        }
    }
    private sealed class LockGate : DbCommandInterceptor, IDisposable
    {
        public ManualResetEventSlim Attempted { get; } = new();
        public ManualResetEventSlim Acquired { get; } = new();
        public ManualResetEventSlim Release { get; } = new();
        private static bool IsLock(DbCommand command) => command.CommandText.Contains("UPDLOCK, HOLDLOCK", StringComparison.Ordinal);
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            if (IsLock(command)) Attempted.Set(); return result;
        }
        public void Dispose() { Attempted.Dispose(); Acquired.Dispose(); Release.Dispose(); }
    }
}

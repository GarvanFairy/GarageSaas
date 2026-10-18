using System.Data.Common;
using GarageSaas.Services;
using GarageSaas.Services.Models;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// Prepared only. No execution without separate approval. Reuses the existing safe
// InvoiceAtomic GUID prefix, but has its own independent fixture opt-in.
[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlInvoiceDeletion")]
public class InvoiceDeletionSqlTests
{
    private static DisposableSqlDatabase? _database;
    private static SqlInvoiceGraph _graph = null!;
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_INVOICE_DELETION_SQL_TESTS", "InvoiceAtomic");
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

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public async Task DirectAttachmentAndDeletionSerializeInEitherOrder(bool attachmentFirst)
    {
        int invoiceId, quoteId;
        using (var seed = new SignupContext(Options()))
        {
            var quote = new WorkQuoteService(seed).AddOrUpdateWorkQuote(new CombinedWorkQuoteWorkitem
            { CustomerId = _graph.CustomerId, VehicleId = _graph.VehicleId, WorkItemId = _graph.WorkItemId, WorkQuoteDate = DateTime.Today }, _graph.GarageId, "seed");
            Assert.IsTrue(quote.Success, quote.ErrorMessage); quoteId = quote.Data.WorkQuoteId;
            var invoice = new VehicleInvoiceService(seed).AddOrUpdateVehicleInvoice(new VehicleInvoice
            { CustomerId = _graph.CustomerId, GarageBusinessCustomerId = _graph.CustomerId, VehicleId = _graph.VehicleId,
                InvoiceDate = DateTime.Today, InvoiceStatus = "Pending", Paid = false }, _graph.GarageId, "seed");
            Assert.IsTrue(invoice.Success, invoice.ErrorMessage); invoiceId = invoice.Data.Id;
        }
        using var saved = new ManualResetEventSlim(); using var release = new ManualResetEventSlim(); using var attempted = new Attempt();
        var first = _database!.Track(Task.Run(() =>
        {
            using var db = new GateContext(Options());
            if (attachmentFirst)
            {
                // C3b2b UPDATE now owns the transaction. Stage its save before
                // service commit, rather than introducing a nested transaction.
                db.AfterSave = () => { saved.Set(); if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Attachment gate"); };
                return Attach(db, invoiceId, quoteId);
            }
            db.AfterSave = () => { saved.Set(); if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Deletion gate"); };
            return new VehicleInvoiceService(db).DeleteVehicleInvoice(invoiceId, _graph.GarageId);
        }));
        Task<ServiceResult>? second = null;
        try
        {
            Assert.IsTrue(saved.Wait(TimeSpan.FromSeconds(10)));
            second = _database.Track(Task.Run(() =>
            {
                using var db = new SignupContext(Options(attempted));
                return attachmentFirst ? new VehicleInvoiceService(db).DeleteVehicleInvoice(invoiceId, _graph.GarageId) : Attach(db, invoiceId, quoteId);
            }));
            Assert.IsTrue(attempted.Started.Wait(TimeSpan.FromSeconds(5))); await Task.Delay(300);
            Assert.IsFalse(second.IsCompleted, "Competing writer bypassed the invoice lock");
        }
        finally { release.Set(); await _database.DrainAsync(); }
        Assert.IsTrue((await first.WaitAsync(TimeSpan.FromSeconds(10))).Success);
        Assert.IsNotNull(second); var outcome = await second.WaitAsync(TimeSpan.FromSeconds(10)); Assert.IsFalse(outcome.Success);
        Assert.AreEqual(attachmentFirst ? "This invoice cannot be deleted because it is linked to one or more work quotes." :
            "Vehicle invoice not found.", outcome.ErrorMessage);
        using var check = new SignupContext(Options());
        if (attachmentFirst) Assert.AreEqual(quoteId, check.VehicleInvoice.Single(x => x.Id == invoiceId).WorkQuoteId);
        else Assert.IsFalse(check.VehicleInvoice.Any(x => x.Id == invoiceId));
        Assert.IsTrue(check.WorkQuote.Any(x => x.Id == quoteId));
    }

    private static ServiceResult Attach(SignupContext db, int invoiceId, int quoteId)
    {
        // Construct the submitted request rather than preloading a tracked invoice.
        return new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(new VehicleInvoice
        { Id = invoiceId, CustomerId = _graph.CustomerId, GarageBusinessCustomerId = _graph.CustomerId,
            VehicleId = _graph.VehicleId, WorkQuoteId = quoteId, InvoiceStatus = "Pending", InvoiceDate = DateTime.Today }, _graph.GarageId, "attach");
    }
    private sealed class GateContext : SignupContext
    {
        public Action? AfterSave;
        public GateContext(DbContextOptions<SignupContext> options) : base(options) { }
        public override int SaveChanges() { var result = base.SaveChanges(); AfterSave?.Invoke(); return result; }
    }
    private sealed class Attempt : DbCommandInterceptor, IDisposable
    {
        public ManualResetEventSlim Started { get; } = new();
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        { if (command.CommandText.Contains("[VehicleInvoice]")) Started.Set(); return result; }
        public void Dispose() => Started.Dispose();
    }
}

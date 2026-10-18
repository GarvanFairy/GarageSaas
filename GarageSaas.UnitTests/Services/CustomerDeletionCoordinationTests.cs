using GarageSaas.Services;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;
namespace GarageSaas.UnitTests.Services;

[TestClass]
public class CustomerDeletionCoordinationTests
{
    private static readonly InMemoryDatabaseRoot Root = new();
    [DataTestMethod]
    [DataRow("item")] [DataRow("quote")] [DataRow("invoice")] [DataRow("convert")]
    public void PendingReferencesCannotBypassCustomerCoordination(string operation)
        => CustomerDeletionCoordinationCases.Pending(new DbContextOptionsBuilder<SignupContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), Root).Options, operation);
    [DataTestMethod]
    [DataRow("before")] [DataRow("after")] [DataRow("update-after")] [DataRow("commit-uncertainty")]
    public void WorkItemTransactionCleanupAndUncertainCommit(string fault)
        => CustomerDeletionCoordinationCases.Check(new DbContextOptionsBuilder<SignupContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), Root)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options, fault, false);
}
internal static class CustomerDeletionCoordinationCases
{
    internal static void Pending(DbContextOptions<SignupContext> options, string operation)
    {
        SqlInvoiceGraph a; using (var seed = new SignupContext(options)) a = SqlInvoiceGraph.Seed(seed);
        using var db = new SignupContext(options);
        var pending = new WorkItem { CustomerId = a.CustomerId, VehicleId = a.VehicleId, GarageBusinessCustomerId = a.GarageId };
        db.Add(pending);
        GarageSaas.Services.Models.ServiceResult result = operation switch
        {
            "item" => new WorkItemService(db).AddOrUpdateWorkItem(new WorkItem { CustomerId = a.CustomerId, VehicleId = a.VehicleId }, a.GarageId, "actor"),
            "quote" => new WorkQuoteService(db).AddOrUpdateWorkQuote(new CombinedWorkQuoteWorkitem
                { CustomerId = a.CustomerId, VehicleId = a.VehicleId, WorkItemId = a.WorkItemId }, a.GarageId, "actor"),
            "invoice" => new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(new VehicleInvoice { CustomerId = a.CustomerId, VehicleId = a.VehicleId }, a.GarageId, "actor"),
            _ => new VehicleInvoiceService(db).CreateFromWorkQuote(1, a.GarageId, "actor")
        };
        Assert.IsFalse(result.Success); StringAssert.Contains(result.ErrorMessage, "Pending changes");
        Assert.AreEqual(EntityState.Added, db.Entry(pending).State);
        using var check = new SignupContext(options); Assert.AreEqual(1, check.WorkItem.Count());
    }
    internal static void Check(DbContextOptions<SignupContext> options, string fault, bool sql)
    {
        SqlInvoiceGraph a; using (var seed = new SignupContext(options)) a = SqlInvoiceGraph.Seed(seed);
        using var db = new FaultContext(options, fault); var before = OwnershipCases.Snapshot(db);
        var error = Assert.ThrowsException<Exception>(() =>
        {
            // MSTest's exact exception type assertion is applied outside this delegate below.
            try { new WorkItemService(db).AddOrUpdateWorkItem(new WorkItem { Id = fault == "update-after" ? a.WorkItemId : 0,
                CustomerId = a.CustomerId, VehicleId = a.VehicleId, RepairInstructions = "Changed" }, a.GarageId, "writer"); }
            catch (Exception e) { throw new Exception("captured", e); }
        }).InnerException!;
        Assert.AreEqual(1, db.Saves); Assert.AreEqual(fault == "commit-uncertainty" ? 1 : 0, db.Commits);
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
        if (fault == "commit-uncertainty")
        {
            Assert.IsInstanceOfType<InvalidOperationException>(error); StringAssert.Contains(error.Message, "could not be confirmed");
            using var committed = new SignupContext(options); Assert.AreEqual(2, committed.WorkItem.Count());
        }
        else
        {
            Assert.IsInstanceOfType<DbUpdateException>(error);
            if (db.Created != null) Assert.AreEqual(0, db.Created.Id);
            db.Fault = "none"; db.SaveChanges();
            if (sql) { using var check = new SignupContext(options); Assert.AreEqual(before, OwnershipCases.Snapshot(check)); }
        }
    }
    private sealed class FaultContext : SignupContext
    {
        private readonly Mock<DatabaseFacade> _facade;
        internal string Fault; internal int Saves, Commits; internal WorkItem? Created;
        public override DatabaseFacade Database => _facade?.Object ?? base.Database;
        internal FaultContext(DbContextOptions<SignupContext> options, string fault) : base(options)
        {
            Fault = fault; _facade = new Mock<DatabaseFacade>(this) { CallBase = true };
            _facade.Setup(x => x.BeginTransaction()).Returns(() =>
            {
                var real = base.Database.BeginTransaction(); var proxy = new Mock<IDbContextTransaction>();
                proxy.Setup(x => x.Commit()).Callback(() => { Commits++; real.Commit(); if (Fault == "commit-uncertainty") throw new IOException("Injected lost acknowledgement"); });
                proxy.Setup(x => x.Rollback()).Callback(() => real.Rollback()); proxy.Setup(x => x.Dispose()).Callback(() => real.Dispose()); return proxy.Object;
            });
        }
        public override int SaveChanges()
        {
            Saves++; Created = ChangeTracker.Entries<WorkItem>().FirstOrDefault(x => x.State == EntityState.Added)?.Entity;
            if (Fault == "before") throw new DbUpdateException("Injected before save");
            var count = base.SaveChanges(); if (Fault is "after" or "update-after") throw new DbUpdateException("Injected after save"); return count;
        }
    }
}


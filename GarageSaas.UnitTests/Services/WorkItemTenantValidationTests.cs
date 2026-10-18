using GarageSaas.Services;
using GarageSaas.Services.Models;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.Tests.Services;

[TestClass]
public class WorkItemTenantValidationTests
{
    private static SignupContext Database()
    {
        var db = DbContextFactory.Create(); WorkItemServiceTests.SeedRelationships(db); return db;
    }
    private static WorkItem Item(int id = 1, int? vehicle = 100, int? customer = 10, int garage = 5) => new()
    {
        Id = id, GarageBusinessCustomerId = garage, VehicleId = vehicle, CustomerId = customer,
        RepairInstructions = "Original", CreatedBy = "seed", CreatedDate = new DateTime(2020, 1, 1),
        UpdatedBy = "old", UpdatedDate = new DateTime(2021, 1, 1)
    };
    private static void Unchanged(SignupContext db, WorkItem expected)
    {
        var saved = db.WorkItem.AsNoTracking().Single();
        Assert.AreEqual(expected.VehicleId, saved.VehicleId); Assert.AreEqual(expected.CustomerId, saved.CustomerId);
        Assert.AreEqual(expected.RepairInstructions, saved.RepairInstructions); Assert.AreEqual(expected.CreatedBy, saved.CreatedBy);
        Assert.AreEqual(expected.CreatedDate, saved.CreatedDate); Assert.AreEqual(expected.UpdatedBy, saved.UpdatedBy);
        Assert.AreEqual(expected.UpdatedDate, saved.UpdatedDate); Assert.AreEqual(expected.GarageBusinessCustomerId, saved.GarageBusinessCustomerId);
        Assert.AreEqual(1, db.WorkItem.Count());
        Assert.IsFalse(db.ChangeTracker.HasChanges());
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(-1)] [DataRow(12345)]
    public void InvalidGarageRejectedByEveryMethod(int garage)
    {
        using var db = Database(); var service = new WorkItemService(db);
        Assert.IsFalse(service.GetWorkItem(1, garage).Success);
        Assert.IsFalse(service.GetWorkItemsForVehicle(100, garage).Success);
        Assert.IsFalse(service.GetAvailableWorkItemsForQuote(garage).Success);
        Assert.IsFalse(service.AddOrUpdateWorkItem(Item(0), garage, "actor").Success);
        Assert.IsFalse(service.AddOrUpdateWorkItemForQuote(new CreateWorkItemRequest { VehicleId = 100, CustomerId = 10, RepairInstructions = "work" }, garage, "actor").Success);
        Assert.AreEqual(0, db.WorkItem.Count());
    }

    [DataTestMethod]
    [DataRow(900, 10)] [DataRow(999, 10)] [DataRow(100, 90)] [DataRow(100, 999)]
    [DataRow(100, 0)] [DataRow(100, -1)] [DataRow(-1, 10)]
    public void CreationRejectsMissingForeignAndInvalidReferences(int vehicle, int customer)
    {
        using var db = Database(); var service = new WorkItemService(db);
        Assert.IsFalse(service.AddOrUpdateWorkItem(Item(0, vehicle, customer), 5, "actor").Success);
        Assert.IsFalse(service.AddOrUpdateWorkItemForQuote(new CreateWorkItemRequest { VehicleId = vehicle, CustomerId = customer, RepairInstructions = "work" }, 5, "actor").Success);
        Assert.AreEqual(0, db.WorkItem.Count()); Assert.IsFalse(db.ChangeTracker.HasChanges());
    }

    [TestMethod]
    public void ForeignAndMissingReferencesHaveSameSafeError()
    {
        using var db = Database(); var service = new WorkItemService(db);
        Assert.AreEqual(service.AddOrUpdateWorkItem(Item(0, 900), 5, "actor").ErrorMessage,
            service.AddOrUpdateWorkItem(Item(0, 999), 5, "actor").ErrorMessage);
        Assert.AreEqual(service.AddOrUpdateWorkItem(Item(0, 100, 90), 5, "actor").ErrorMessage,
            service.AddOrUpdateWorkItem(Item(0, 100, 999), 5, "actor").ErrorMessage);
    }

    [DataTestMethod]
    [DataRow(900, 10)] [DataRow(999, 10)] [DataRow(100, 90)] [DataRow(100, 999)]
    public void GeneralUpdateRejectsReplacementWithoutChangingTrackedValues(int vehicle, int customer)
    {
        using var db = Database(); db.WorkItem.Add(Item()); db.SaveChanges();
        Assert.IsFalse(new WorkItemService(db).AddOrUpdateWorkItem(new WorkItem { Id = 1, VehicleId = vehicle, CustomerId = customer, RepairInstructions = "forged" }, 5, "attacker").Success);
        Unchanged(db, Item());
    }

    [TestMethod]
    public void ForeignPrimaryUpdateDeniedOnBothPaths()
    {
        using var db = Database(); db.WorkItem.Add(Item(1, 900, 90, 99)); db.SaveChanges();
        var service = new WorkItemService(db);
        Assert.IsFalse(service.GetWorkItem(1, 5).Success);
        Assert.IsFalse(service.AddOrUpdateWorkItem(new WorkItem { Id = 1, VehicleId = 100, CustomerId = 10 }, 5, "attacker").Success);
        Assert.IsFalse(service.AddOrUpdateWorkItemForQuote(new CreateWorkItemRequest { Id = 1, VehicleId = 100, CustomerId = 10, RepairInstructions = "forged" }, 5, "attacker").Success);
        Unchanged(db, Item(1, 900, 90, 99));
    }

    [TestMethod]
    public void GeneralCreationSupportsGarageOwnedVehicleAndNullCustomerWithoutNewMapping()
    {
        using var db = Database(); var result = new WorkItemService(db).AddOrUpdateWorkItem(Item(0, 200, null), 5, "actor");
        Assert.IsTrue(result.Success); Assert.IsNull(result.Data.CustomerId); Assert.AreEqual(200, result.Data.VehicleId);
    }

    [DataTestMethod]
    [DataRow(900, 90)] [DataRow(0, 0)] [DataRow(-1, -1)]
    public void QuoteDescriptionUpdateIgnoresSubmittedRelationships(int vehicle, int customer)
    {
        using var db = Database(); db.WorkItem.Add(Item()); db.SaveChanges();
        var result = new WorkItemService(db).AddOrUpdateWorkItemForQuote(new CreateWorkItemRequest { Id = 1, VehicleId = vehicle, CustomerId = customer, RepairInstructions = " New instructions " }, 5, "actor");
        Assert.IsTrue(result.Success); Assert.AreEqual(100, result.Data.VehicleId); Assert.AreEqual(10, result.Data.CustomerId);
        Assert.AreEqual("New instructions", result.Data.RepairInstructions); Assert.AreEqual("actor", result.Data.UpdatedBy);
    }

    [TestMethod]
    public void HistoricalNullReferencesRemainReadableAndDescriptionEditable()
    {
        using var db = Database(); db.WorkItem.Add(Item(1, null, null)); db.SaveChanges();
        var service = new WorkItemService(db);
        Assert.IsTrue(service.GetWorkItem(1, 5).Success);
        Assert.AreEqual(1, service.GetAvailableWorkItemsForQuote(5).Data.Count);
        Assert.IsTrue(service.AddOrUpdateWorkItemForQuote(new CreateWorkItemRequest { Id = 1, RepairInstructions = "history" }, 5, "actor").Success);
        Assert.IsNull(db.WorkItem.Single().VehicleId); Assert.IsNull(db.WorkItem.Single().CustomerId);
    }

    [DataTestMethod]
    [DataRow(900, 10)] [DataRow(999, 10)] [DataRow(100, 90)] [DataRow(100, 999)] [DataRow(100, 0)]
    public void UnsafeHistoricalReferencesDeniedWithoutRepair(int vehicle, int customer)
    {
        using var db = Database(); db.WorkItem.Add(Item(1, vehicle, customer)); db.SaveChanges();
        var service = new WorkItemService(db);
        Assert.IsFalse(service.GetWorkItem(1, 5).Success);
        Assert.AreEqual(0, service.GetAvailableWorkItemsForQuote(5).Data.Count);
        Assert.IsFalse(service.AddOrUpdateWorkItem(Item(), 5, "actor").Success);
        Assert.IsFalse(service.AddOrUpdateWorkItemForQuote(new CreateWorkItemRequest { Id = 1, RepairInstructions = "forged" }, 5, "actor").Success);
        Unchanged(db, Item(1, vehicle, customer));
    }

    [DataTestMethod]
    [DataRow("quote")] [DataRow("invoice")] [DataRow("foreign-link")] [DataRow("orphan-link")]
    public void LinkedItemsCannotBeRetargetedButDescriptionsRemainEditable(string linkType)
    {
        using var db = Database(); db.WorkItem.Add(Item());
        db.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem { WorkItemId = 1, WorkQuoteId = 50, GarageBusinessCustomerId = linkType == "foreign-link" ? 99 : 5 });
        if (linkType != "orphan-link") db.WorkQuote.Add(new WorkQuote { Id = 50, GarageBusinessCustomerId = 5, VehicleId = 100, CustomerId = 10 });
        if (linkType == "invoice")
        {
            db.VehicleInvoice.Add(new VehicleInvoice { Id = 60, GarageBusinessId = 5, WorkQuoteId = 50, VehicleId = 100, CustomerId = 10 });
            db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 60, WorkQuoteId = 50, GarageBusinessCustomerId = 5 });
        }
        db.SaveChanges(); var service = new WorkItemService(db);
        Assert.IsFalse(service.AddOrUpdateWorkItem(new WorkItem { Id = 1, VehicleId = 200, CustomerId = 20, RepairInstructions = "retarget" }, 5, "actor").Success);
        Unchanged(db, Item());
        Assert.IsTrue(service.AddOrUpdateWorkItem(new WorkItem { Id = 1, VehicleId = 100, CustomerId = 10, RepairInstructions = "description" }, 5, "actor").Success);
        Assert.AreEqual(1, db.WorkQuoteWorkItem.Count());
    }

    [TestMethod]
    public void ReadsFilterInconsistentReferencesAndDoNotTrackEntities()
    {
        using var db = Database();
        db.WorkItem.AddRange(Item(1), Item(2, 100, 90), Item(3, 900), Item(4, 100, null), Item(5, 100, 999), Item(6, 100, 10, 99)); db.SaveChanges();
        foreach (var entry in db.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
        var service = new WorkItemService(db);
        CollectionAssert.AreEqual(new[] { 1, 4 }, service.GetWorkItemsForVehicle(100, 5).Data.Select(x => x.Id).ToArray());
        CollectionAssert.AreEqual(new[] { 4, 1 }, service.GetAvailableWorkItemsForQuote(5).Data.Select(x => x.Id).ToArray());
        Assert.IsTrue(service.GetWorkItem(1, 5).Success);
        Assert.IsFalse(service.GetWorkItemsForVehicle(900, 5).Success);
        Assert.IsFalse(service.GetWorkItemsForVehicle(999, 5).Success);
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
    }

    [TestMethod]
    public void HistoricalCustomerNeedNotBeCurrentVehicleOwnerOrActive()
    {
        using var db = Database();
        db.CustomerOwnedVehicles.RemoveRange(((IQueryable<CustomerOwnedVehicles>)db.CustomerOwnedVehicles).Where(x => x.VehicleId == 100));
        db.CustomerOwnedVehicles.Add(new CustomerOwnedVehicles { GarageBusinessId = 5, VehicleId = 100, GarageBusinessCustomerId = 20 });
        db.GarageBusinessCustomer.Single(x => x.Id == 10).Active = false;
        db.CustomerVehicle.Single(x => x.Id == 100).Active = false; db.SaveChanges();
        var service = new WorkItemService(db);
        Assert.IsTrue(service.AddOrUpdateWorkItem(Item(0), 5, "actor").Success);
        Assert.IsTrue(service.AddOrUpdateWorkItemForQuote(new CreateWorkItemRequest { VehicleId = 100, CustomerId = 10, RepairInstructions = "quote" }, 5, "actor").Success);
    }
    [TestMethod]
    public void SameGarageWorkItemQuoteAndInvoiceFlowStillWorks()
    {
        var options = new DbContextOptionsBuilder<SignupContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options;
        using var db = new SignupContext(options); WorkItemServiceTests.SeedRelationships(db);
        // This functional check does not assert SQL Server transaction semantics.
        var created = new WorkItemService(db).AddOrUpdateWorkItem(Item(0), 5, "actor");
        Assert.IsTrue(created.Success);
        var quote = new WorkQuoteService(db).AddOrUpdateWorkQuote(new CombinedWorkQuoteWorkitem
        {
            VehicleId = 100, CustomerId = 10, WorkItemIds = new List<int> { created.Data.Id },
            WorkQuoteDate = DateTime.Today, WorkRequest = "Repair", Labour = 100, Tax = 0
        }, 5, "actor");
        Assert.IsTrue(quote.Success, quote.ErrorMessage);
        var invoice = new VehicleInvoiceService(db).CreateFromWorkQuote(quote.Data.WorkQuoteId, 5, "actor");
        Assert.IsTrue(invoice.Success, invoice.ErrorMessage);
        Assert.AreEqual(100, invoice.Data.VehicleId); Assert.AreEqual(10, invoice.Data.CustomerId);
        Assert.AreEqual(5, invoice.Data.GarageBusinessId);
        Assert.AreEqual(1, db.WorkQuoteWorkItem.Count()); Assert.AreEqual(1, db.InvoiceWorkQuote.Count());
    }}


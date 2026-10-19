using System.Text.Json;
using GarageSaas.Services;
using GarageSaas.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class WorkQuoteTenantValidationTests
{
    internal static SignupContext Database()
    {
        var options = VehicleInvoiceServiceTests.Options();
        using (var seed = new SignupContext(options))
        {
            VehicleInvoiceServiceTests.Seed(seed);
            foreach (var quote in seed.WorkQuote) { quote.CreatedBy = "original"; quote.CreatedDate = new DateTime(2020, 1, 1); quote.UpdatedBy = "previous"; }
            seed.SaveChanges();
        }
        return new SignupContext(options);
    }
    internal static CombinedWorkQuoteWorkitem Input(int id = 0) => new()
    {
        WorkQuoteId = id, CustomerId = 101, VehicleId = 10, WorkItemIds = new() { 1000 },
        WorkQuoteDate = new DateTime(2026, 1, 1), WorkRequest = "Repair", Labour = 100
    };
    private static string State(SignupContext db) => JsonSerializer.Serialize(new
    {
        Quotes = db.WorkQuote.AsNoTracking().OrderBy(x => x.Id).ToList(),
        Links = db.WorkQuoteWorkItem.AsNoTracking().OrderBy(x => x.Id).ToList(),
        Items = db.WorkItem.AsNoTracking().OrderBy(x => x.Id).ToList(),
        Invoices = db.VehicleInvoice.AsNoTracking().OrderBy(x => x.Id).ToList(),
        InvoiceLinks = db.InvoiceWorkQuote.AsNoTracking().OrderBy(x => x.Id).ToList()
    });
    private static void Detach(SignupContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
    }
    private static void Invoice(SignupContext db)
    {
        var invoice = VehicleInvoiceServiceTests.Invoice(); invoice.WorkQuoteId = 100;
        db.VehicleInvoice.Add(invoice);
        db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 100, GarageBusinessCustomerId = 1 });
    }
    internal static void Corrupt(SignupContext db, string kind)
    {
        var quote = db.WorkQuote.Single(x => x.Id == 100);
        var item = db.WorkItem.Single(x => x.Id == 1000);
        var link = db.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == 100);
        switch (kind)
        {
            case "customer": quote.CustomerId = 201; break;
            case "missing-customer": quote.CustomerId = 999; break;
            case "vehicle": quote.VehicleId = 20; break;
            case "missing-vehicle": quote.VehicleId = 999; break;
            case "link-garage": link.GarageBusinessCustomerId = 2; break;
            case "null-link-garage": link.GarageBusinessCustomerId = null; break;
            case "item": link.WorkItemId = 2000; break;
            case "missing-item": link.WorkItemId = 999; break;
            case "zero-item": link.WorkItemId = 0; break;
            case "item-customer": item.CustomerId = 201; break;
            case "missing-item-customer": item.CustomerId = 999; break;
            case "item-vehicle": item.VehicleId = 20; break;
            case "missing-item-vehicle": item.VehicleId = 999; break;
            default:
                Invoice(db);
                var invoiceLink = db.InvoiceWorkQuote.Local.Single(); var invoice = db.VehicleInvoice.Local.Single();
                if (kind == "invoice") invoice.GarageBusinessId = 2;
                if (kind == "missing-invoice") invoiceLink.InvoiceId = 999;
                if (kind == "null-invoice") invoiceLink.InvoiceId = null;
                if (kind == "invoice-link-garage") invoiceLink.GarageBusinessCustomerId = 2;
                if (kind == "invoice-customer") invoice.CustomerId = invoice.GarageBusinessCustomerId = 201;
                if (kind == "invoice-vehicle") invoice.VehicleId = 20;
                if (kind == "invoice-direct-conflict") invoice.WorkQuoteId = 101;
                if (kind == "invoice-other-quote") db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 200, GarageBusinessCustomerId = 1 });
                if (kind == "foreign-direct") db.InvoiceWorkQuote.Remove(invoiceLink);
                if (kind == "foreign-direct") invoice.GarageBusinessId = 2;
                if (kind == "multiple-invoices")
                {
                    var other = VehicleInvoiceServiceTests.Invoice(2); other.WorkQuoteId = 100; db.VehicleInvoice.Add(other);
                }
                break;
        }
        db.SaveChanges(); Detach(db);
    }

    [DataTestMethod]
    [DataRow("customer")] [DataRow("missing-customer")] [DataRow("vehicle")] [DataRow("missing-vehicle")]
    [DataRow("link-garage")] [DataRow("null-link-garage")] [DataRow("item")] [DataRow("missing-item")] [DataRow("zero-item")]
    [DataRow("item-customer")] [DataRow("missing-item-customer")] [DataRow("item-vehicle")] [DataRow("missing-item-vehicle")]
    [DataRow("invoice")] [DataRow("missing-invoice")] [DataRow("null-invoice")] [DataRow("invoice-link-garage")]
    [DataRow("invoice-customer")] [DataRow("invoice-vehicle")] [DataRow("invoice-direct-conflict")]
    [DataRow("invoice-other-quote")] [DataRow("foreign-direct")] [DataRow("multiple-invoices")]
    public void MalformedStoredGraphsCannotBeReadEditedOrDeleted(string kind)
    {
        using var db = Database(); Corrupt(db, kind); var before = State(db);
        var service = new WorkQuoteService(db);
        var single = service.GetWorkQuote(100, 1); Assert.IsFalse(single.Success); Assert.IsNull(single.Data);
        Assert.IsFalse(service.GetWorkQuotes(1).Data.Any(x => x.WorkQuoteId == 100));
        var vehicle = service.GetWorkQuotesForVehicle(10, 1);
        Assert.IsFalse(vehicle.Success && vehicle.Data.Any(x => x.WorkQuoteId == 100));
        var item = service.GetWorkQuotesForWorkItem(1000, 1);
        Assert.IsFalse(item.Success && item.Data.Any(x => x.WorkQuoteId == 100));
        Assert.IsFalse(service.AddOrUpdateWorkQuote(Input(100), 1, "attacker").Success);
        Assert.IsFalse(service.DeleteWorkQuote(100, 1).Success);
        Assert.AreEqual(before, State(db)); Assert.IsFalse(db.ChangeTracker.HasChanges());
        var exposed = JsonSerializer.Serialize(new { single, vehicle, item, list = service.GetWorkQuotes(1) });
        Assert.IsFalse(exposed.Contains("SECRET")); Assert.IsFalse(exposed.Contains("secret@example.com"));
    }

    [DataTestMethod]
    [DataRow(false, "customer", 201)] [DataRow(true, "customer", 201)]
    [DataRow(false, "customer", 999)] [DataRow(true, "customer", 999)]
    [DataRow(false, "vehicle", 20)] [DataRow(true, "vehicle", 20)]
    [DataRow(false, "vehicle", 999)] [DataRow(true, "vehicle", 999)]
    [DataRow(false, "item", 2000)] [DataRow(true, "item", 2000)]
    [DataRow(false, "item", 999)] [DataRow(true, "item", 999)]
    [DataRow(false, "item", 0)] [DataRow(true, "item", -1)]
    public void ForeignMissingAndInvalidSubmissionsNeverMutate(bool update, string field, int id)
    {
        using var db = Database(); var before = State(db); var input = Input(update ? 100 : 0);
        if (field == "customer") input.CustomerId = id;
        if (field == "vehicle") input.VehicleId = id;
        if (field == "item") input.WorkItemIds = new() { id };
        var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "attacker");
        Assert.IsFalse(result.Success); Assert.IsNull(result.Data); Assert.AreEqual(before, State(db)); Assert.IsFalse(db.ChangeTracker.HasChanges());
    }

    [DataTestMethod]
    [DataRow("customer", 201)] [DataRow("customer", 999)] [DataRow("vehicle", 20)] [DataRow("vehicle", 999)]
    public void NewlySelectedWorkItemsMustHaveValidNullableRelationships(string field, int id)
    {
        using var db = Database(); var item = db.WorkItem.Single(x => x.Id == 1001);
        if (field == "customer") item.CustomerId = id; else item.VehicleId = id;
        db.SaveChanges(); Detach(db); var before = State(db);
        foreach (var quoteId in new[] { 0, 100 })
        {
            var input = Input(quoteId); input.WorkItemIds.Add(1001);
            Assert.IsFalse(new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "actor").Success);
            Assert.AreEqual(before, State(db));
        }
    }

    [TestMethod]
    public void MultipleSameGarageItemsCanHaveDifferentCustomersVehiclesAndNullHistory()
    {
        using var db = Database(); db.WorkItem.Add(new WorkItem { Id = 1002, GarageBusinessCustomerId = 1, CustomerId = 102, VehicleId = 11 }); db.SaveChanges(); Detach(db);
        var input = Input(); input.WorkItemIds = new() { 1000, 1001, 1002, 1000 }; input.GarageBusinessCustomerId = 2;
        input.InvoiceId = 999; input.HasInvoice = true; input.WorkItems.Add(new WorkItem { RepairInstructions = "FORGED" });
        var service = new WorkQuoteService(db); var saved = service.AddOrUpdateWorkQuote(input, 1, "actor");
        Assert.IsTrue(saved.Success, saved.ErrorMessage); Assert.AreEqual(1, saved.Data.GarageBusinessCustomerId);
        Assert.AreEqual(3, saved.Data.WorkItemIds.Count); Assert.IsNull(saved.Data.InvoiceId); Assert.IsFalse(saved.Data.HasInvoice);
        Assert.IsFalse(JsonSerializer.Serialize(saved).Contains("FORGED"));
        var read = service.GetWorkQuote(saved.Data.WorkQuoteId, 1); Assert.IsTrue(read.Success); Assert.AreEqual(3, read.Data.WorkItems.Count);
        var edit = Input(saved.Data.WorkQuoteId); edit.WorkItemIds = new() { 1002 }; edit.VehicleId = 11; edit.CustomerId = 102;
        Assert.IsTrue(service.AddOrUpdateWorkQuote(edit, 1, "editor").Success);
        Assert.AreEqual(1, db.WorkQuoteWorkItem.Count(x => x.WorkQuoteId == edit.WorkQuoteId));
    }

    [TestMethod]
    public void LegacySingleItemAndGarageOwnedVehicleArePreserved()
    {
        using var db = Database(); var input = Input(); input.WorkItemIds.Clear(); input.WorkItemId = 1000; input.VehicleId = 11;
        var saved = new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "actor");
        Assert.IsTrue(saved.Success, saved.ErrorMessage); Assert.AreEqual(1000, saved.Data.WorkItemId);
        Assert.AreEqual(11, db.WorkQuote.Single(x => x.Id == saved.Data.WorkQuoteId).VehicleId);
    }

    [DataTestMethod]
    [DataRow("customer")] [DataRow("vehicle")] [DataRow("mixed-items")]
    public void ZeroReferencesAreNotConvertedToNullOrSilentlyDropped(string kind)
    {
        using var db = Database(); var before = State(db); var input = Input();
        input.VehicleId = 11; // Garage-owned still requires a positive customer on save.
        if (kind == "customer") input.CustomerId = 0;
        if (kind == "vehicle") input.VehicleId = 0;
        if (kind == "mixed-items") input.WorkItemIds.Add(0);
        Assert.IsFalse(new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "actor").Success);
        Assert.AreEqual(before, State(db));
    }

    [TestMethod]
    public void OwnershipLookupRejectsMislabelledRowsAndDropdownsStayScoped()
    {
        using var db = Database();
        db.VehicleMake.Add(new VehicleMake { Id = 1, Make = "Shared make" });
        db.VehicleModel.Add(new VehicleModel { Id = 1, MakeId = 1, Model = "Shared model" });
        var vehicle = db.CustomerVehicle.Single(x => x.Id == 10); vehicle.VehicleMakeId = 1; vehicle.VehicleModelId = 1;
        db.CustomerOwnedVehicles.Single().GarageBusinessId = 2; db.SaveChanges(); Detach(db);
        var service = new WorkQuoteService(db);
        CollectionAssert.AreEquivalent(new[] { 11 }, service.GetVehiclesForGarageCustomer(101, 1).Data.Select(v => v.Id).ToArray(), "Only the garage-owned vehicle remains selectable when ownership is mislabelled.");
        Assert.IsFalse(service.GetVehiclesForGarageCustomer(201, 1).Success);
        Assert.IsFalse(service.GetCustomerDropdownItems(1).Any(x => x.Value == "201"));
        Assert.IsFalse(service.GetVehicleDropdownItems(1).Any(x => x.Value == "20"));
        db.CustomerOwnedVehicles.Single().GarageBusinessId = 1; db.SaveChanges(); Detach(db);
        CollectionAssert.AreEquivalent(new[] { 10, 11 }, service.GetVehiclesForGarageCustomer(101, 1).Data.Select(v => v.Id).ToArray());
    }

    [TestMethod]
    public void LegacyCreatorRetainsNullCustomerAndNormalSameGarageBehavior()
    {
        using var db = Database();
        var result = new WorkQuoteService(db).CreateWorkItemForQuote(new CreateWorkItemRequest
            { VehicleId = 10, CustomerId = 201, RepairInstructions = "  Legacy instructions  " }, 1);
        Assert.IsTrue(result.Success); Assert.AreEqual(1, result.Data.GarageBusinessCustomerId);
        Assert.AreEqual(10, result.Data.VehicleId); Assert.IsNull(result.Data.CustomerId, "Legacy ignored customer input must not become a stored foreign reference.");
        Assert.AreEqual("Legacy instructions", result.Data.RepairInstructions);
    }

    [TestMethod]
    public void NullableHistoricalGraphIsReadableWithoutTrackingOrRepair()
    {
        using var db = Database(); db.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem { GarageBusinessCustomerId = 1, WorkQuoteId = 101, WorkItemId = null }); db.SaveChanges(); Detach(db);
        var before = State(db); var service = new WorkQuoteService(db); var read = service.GetWorkQuote(101, 1);
        Assert.IsTrue(read.Success); Assert.AreEqual(0, read.Data.CustomerId); Assert.AreEqual(0, read.Data.VehicleId);
        Assert.AreEqual(1, read.Data.WorkItems.Count); Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
        read.Data.WorkItems[0].RepairInstructions = "changed in memory"; db.SaveChanges();
        Assert.AreEqual(before, State(db));
    }

    [TestMethod]
    public void DirectOnlyAndMultipleQuoteAssociationsProtectRelationshipsButAllowTextEdits()
    {
        using var db = Database(); var invoice = VehicleInvoiceServiceTests.Invoice(); invoice.WorkQuoteId = 100;
        db.VehicleInvoice.Add(invoice); db.SaveChanges(); Detach(db); var service = new WorkQuoteService(db);
        var read = service.GetWorkQuote(100, 1); Assert.IsTrue(read.Success); Assert.IsTrue(read.Data.HasInvoice); Assert.AreEqual(1, read.Data.InvoiceId);
        db.InvoiceWorkQuote.AddRange(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 100, GarageBusinessCustomerId = 1 },
            new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 101, GarageBusinessCustomerId = 1 },
            new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = null, GarageBusinessCustomerId = 1 }); db.SaveChanges(); Detach(db);
        Assert.AreEqual(1, service.GetWorkQuote(101, 1).Data.InvoiceId);
        var edit = Input(100); edit.VehicleId = 11; edit.CustomerId = 102; edit.Comment = "Ordinary linked edit";
        // C3a deliberately supersedes C1/C2b linked retargeting/deletion policy.
        Assert.IsFalse(service.AddOrUpdateWorkQuote(edit, 1, "actor").Success);
        edit = Input(100); edit.Comment = "Ordinary linked edit";
        Assert.IsTrue(service.AddOrUpdateWorkQuote(edit, 1, "actor").Success);
        Assert.IsFalse(service.DeleteWorkQuote(100, 1).Success);
        Assert.IsTrue(db.VehicleInvoice.Any(x => x.Id == 1)); Assert.AreEqual(3, db.InvoiceWorkQuote.Count());
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(-1)] [DataRow(999)]
    public void EveryPublicDatabaseMethodRejectsInvalidGarage(int garage)
    {
        using var db = Database(); var service = new WorkQuoteService(db); var before = State(db);
        Assert.IsFalse(service.GetWorkQuote(100, garage).Success); Assert.IsFalse(service.GetWorkQuotes(garage).Success);
        Assert.IsFalse(service.GetWorkQuotesForVehicle(10, garage).Success); Assert.IsFalse(service.GetWorkQuotesForWorkItem(1000, garage).Success);
        Assert.IsFalse(service.AddOrUpdateWorkQuote(Input(), garage, "actor").Success); Assert.IsFalse(service.DeleteWorkQuote(100, garage).Success);
        Assert.IsFalse(service.GetVehiclesForGarageCustomer(101, garage).Success);
        Assert.IsFalse(service.CreateWorkItemForQuote(new CreateWorkItemRequest { VehicleId = 10, RepairInstructions = "test" }, garage).Success);
        Assert.AreEqual(0, service.GetCustomerDropdownItems(garage).Count); Assert.AreEqual(0, service.GetVehicleDropdownItems(garage).Count);
        Assert.AreEqual(before, State(db));
    }

    [DataTestMethod]
    [DataRow(20, 2000)] [DataRow(999, 999)]
    public void ForeignOrMissingRequestedRootsAndLegacyVehicleCreationAreRejected(int vehicle, int item)
    {
        using var db = Database(); var service = new WorkQuoteService(db); var before = State(db);
        Assert.IsFalse(service.GetWorkQuotesForVehicle(vehicle, 1).Success); Assert.IsFalse(service.GetWorkQuotesForWorkItem(item, 1).Success);
        Assert.IsFalse(service.CreateWorkItemForQuote(new CreateWorkItemRequest { VehicleId = vehicle, RepairInstructions = "test" }, 1).Success);
        Assert.AreEqual(before, State(db));
        Assert.IsFalse(service.GetWorkQuote(200, 1).Success); Assert.IsFalse(service.AddOrUpdateWorkQuote(Input(200), 1, "actor").Success);
        Assert.IsFalse(service.DeleteWorkQuote(200, 1).Success); Assert.AreEqual(before, State(db));
    }
}

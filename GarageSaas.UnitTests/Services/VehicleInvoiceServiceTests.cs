using System.Text.Json;
using GarageSaas.Services;
using GarageSaas.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class VehicleInvoiceServiceTests
{
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new InMemoryDatabaseRoot();
    internal static DbContextOptions<SignupContext> Options() => new DbContextOptionsBuilder<SignupContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString(), DatabaseRoot)
        // Functional conversion tests only; these do not assert SQL transactions.
        .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options;

    internal static void Seed(SignupContext db)
    {
        db.GarageBusiness.AddRange(new GarageBusiness { Id = 1, GarageBusinessName = "Local", Active = true }, new GarageBusiness { Id = 2, GarageBusinessName = "Foreign", Active = true });
        db.GarageBusinessCustomer.AddRange(
            new GarageBusinessCustomer { Id = 101, GarageBusinessId = 1, GarageCustomerForename = "Local", GarageCustomerSurname = "Customer" },
            new GarageBusinessCustomer { Id = 102, GarageBusinessId = 1, GarageCustomerForename = "Historical", GarageCustomerSurname = "Customer" },
            new GarageBusinessCustomer { Id = 201, GarageBusinessId = 2, GarageCustomerForename = "SECRET CUSTOMER", GarageCustomerEmailAddress = "secret@example.com" });
        db.CustomerVehicle.AddRange(new CustomerVehicle { Id = 10, GarageBusinessId = 1, VehicleRegistration = "LOCAL", GarageOwned = false },
            new CustomerVehicle { Id = 11, GarageBusinessId = 1, VehicleRegistration = "GARAGE OWNED", GarageOwned = true },
            new CustomerVehicle { Id = 20, GarageBusinessId = 2, VehicleRegistration = "SECRET VEHICLE" });
        db.CustomerOwnedVehicles.Add(new CustomerOwnedVehicles { GarageBusinessId = 1, VehicleId = 10, GarageBusinessCustomerId = 101 });
        db.WorkItem.AddRange(new WorkItem { Id = 1000, GarageBusinessCustomerId = 1, VehicleId = 10, CustomerId = 101, RepairInstructions = "Local work" },
            new WorkItem { Id = 1001, GarageBusinessCustomerId = 1, VehicleId = null, CustomerId = null, RepairInstructions = "History" },
            new WorkItem { Id = 2000, GarageBusinessCustomerId = 2, VehicleId = 20, CustomerId = 201, RepairInstructions = "SECRET WORK" });
        db.WorkQuote.AddRange(new WorkQuote { Id = 100, GarageBusinessCustomerId = 1, VehicleId = 10, CustomerId = 101, Labour = 100 },
            new WorkQuote { Id = 101, GarageBusinessCustomerId = 1, VehicleId = null, CustomerId = null },
            new WorkQuote { Id = 200, GarageBusinessCustomerId = 2, VehicleId = 20, CustomerId = 201 });
        db.WorkQuoteWorkItem.AddRange(new WorkQuoteWorkItem { GarageBusinessCustomerId = 1, WorkQuoteId = 100, WorkItemId = 1000 },
            new WorkQuoteWorkItem { GarageBusinessCustomerId = 1, WorkQuoteId = 101, WorkItemId = 1001 });
        db.SaveChanges();
    }

    internal static VehicleInvoice Invoice(int id = 1) => new()
    {
        Id = id, GarageBusinessId = 1, CustomerId = 101, GarageBusinessCustomerId = 101, VehicleId = 10,
        InvoiceStatus = "Pending", InvoiceDate = new DateTime(2020, 1, 1), Labour = 100, Total = 123,
        Comment = "Original", CreatedBy = "seed", CreatedDate = new DateTime(2020, 1, 1),
        UpdatedBy = "previous", UpdatedDate = new DateTime(2021, 1, 1), Paid = false
    };
    private static SignupContext Database(bool invoice = true)
    {
        var db = new SignupContext(Options()); Seed(db);
        if (invoice) { db.VehicleInvoice.Add(Invoice()); db.SaveChanges(); }
        return db;
    }
    private static string State(SignupContext db) => JsonSerializer.Serialize(db.VehicleInvoice.AsNoTracking().OrderBy(x => x.Id).ToList());
    private static void Unchanged(SignupContext db, string before)
    {
        Assert.AreEqual(before, State(db)); Assert.IsFalse(db.ChangeTracker.HasChanges());
    }

    [TestMethod]
    public void SameGarageCreationAndEditingRetainContractsAndServerTenant()
    {
        using var db = Database(false); var service = new VehicleInvoiceService(db);
        var input = Invoice(0); input.GarageBusinessId = 2;
        var created = service.AddOrUpdateVehicleInvoice(input, 1, "actor"); Assert.IsTrue(created.Success, created.ErrorMessage);
        Assert.AreEqual(1, created.Data.GarageBusinessId); StringAssert.StartsWith(created.Data.InvoiceNumber, "INV-");
        var edit = Invoice(created.Data.Id); edit.CustomerId = 102; edit.GarageBusinessCustomerId = 102; edit.VehicleId = 11; edit.Labour = 200;
        Assert.IsTrue(service.AddOrUpdateVehicleInvoice(edit, 1, "editor").Success);
        Assert.AreEqual(102, db.VehicleInvoice.Single().CustomerId); Assert.AreEqual(11, db.VehicleInvoice.Single().VehicleId);
        Assert.AreEqual("editor", db.VehicleInvoice.Single().UpdatedBy);
    }

    public static IEnumerable<object[]> InvalidReferences()
    {
        foreach (var field in new[] { "customer", "alias", "vehicle", "quote" })
        foreach (var kind in new[] { "foreign", "missing", "zero", "negative" })
            yield return new object[] { field, kind };
    }
    private static void Forge(VehicleInvoice invoice, string field, string kind)
    {
        var id = kind == "zero" ? 0 : kind == "negative" ? -1 : kind == "missing" ? 9999 : field == "vehicle" ? 20 : field == "quote" ? 200 : 201;
        switch (field)
        {
            case "customer": invoice.CustomerId = id; invoice.GarageBusinessCustomerId = null; break;
            case "alias": invoice.GarageBusinessCustomerId = id; invoice.CustomerId = null; break;
            case "vehicle": invoice.VehicleId = id; break;
            case "quote": invoice.WorkQuoteId = id; break;
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(InvalidReferences), DynamicDataSourceType.Method)]
    public void InvalidReferencesCannotCreateOrUpdateAndLeaveAllValuesUnchanged(string field, string kind)
    {
        using var db = Database(); var service = new VehicleInvoiceService(db); var before = State(db);
        var create = Invoice(0); Forge(create, field, kind);
        var edit = Invoice(); Forge(edit, field, kind); edit.Labour = 999; edit.InvoiceStatus = "Paid"; edit.Comment = "forged";
        Assert.IsFalse(service.AddOrUpdateVehicleInvoice(create, 1, "attacker").Success);
        Assert.IsFalse(service.AddOrUpdateVehicleInvoice(edit, 1, "attacker").Success);
        Unchanged(db, before);
    }

    [TestMethod]
    public void CustomerAliasesMustAgreeAndForeignMissingErrorsAreEquivalent()
    {
        using var db = Database(); var service = new VehicleInvoiceService(db); var before = State(db);
        var mismatch = Invoice(); mismatch.CustomerId = 102;
        Assert.IsFalse(service.AddOrUpdateVehicleInvoice(mismatch, 1, "actor").Success); Unchanged(db, before);
        foreach (var field in new[] { "customer", "alias", "vehicle", "quote" })
        {
            var foreign = Invoice(0); Forge(foreign, field, "foreign"); var missing = Invoice(0); Forge(missing, field, "missing");
            Assert.AreEqual(service.AddOrUpdateVehicleInvoice(foreign, 1, "actor").ErrorMessage, service.AddOrUpdateVehicleInvoice(missing, 1, "actor").ErrorMessage);
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(InvalidReferences), DynamicDataSourceType.Method)]
    public void UnsafeInvoicesDoNotAppearInDetailsOrLists(string field, string kind)
    {
        using var db = Database(false); var invoice = Invoice(); Forge(invoice, field, kind); db.VehicleInvoice.Add(invoice); db.SaveChanges();
        var service = new VehicleInvoiceService(db);
        Assert.IsFalse(service.GetVehicleInvoice(1, 1).Success);
        Assert.AreEqual(0, service.GetInvoicesByGarageBusinessId(1).Data.Count);
        Assert.AreEqual(0, service.GetInvoicesByGarageCustomerId(1, 101).Data.Count);
    }

    [DataTestMethod]
    [DataRow("foreign-link")][DataRow("foreign-quote")][DataRow("missing-quote")][DataRow("direct-conflict")]
    [DataRow("foreign-work-link")][DataRow("foreign-work")][DataRow("missing-work")][DataRow("foreign-work-customer")][DataRow("foreign-work-vehicle")]
    public void InconsistentGraphsAreHiddenAndCannotBeMutated(string kind)
    {
        using var db = Database();
        db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = kind == "foreign-quote" ? 200 : kind == "missing-quote" ? 999 : 100, GarageBusinessCustomerId = kind == "foreign-link" ? 2 : 1 });
        if (kind == "direct-conflict") db.VehicleInvoice.Single().WorkQuoteId = 101;
        var workLink = db.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == 100);
        if (kind == "foreign-work-link") workLink.GarageBusinessCustomerId = 2;
        if (kind == "foreign-work") workLink.WorkItemId = 2000;
        if (kind == "missing-work") workLink.WorkItemId = 999;
        if (kind == "foreign-work-customer") db.WorkItem.Single(x => x.Id == 1000).CustomerId = 201;
        if (kind == "foreign-work-vehicle") db.WorkItem.Single(x => x.Id == 1000).VehicleId = 20;
        db.SaveChanges(); var before = State(db); var service = new VehicleInvoiceService(db);
        Assert.IsFalse(service.GetVehicleInvoice(1, 1).Success);
        Assert.AreEqual(0, service.GetInvoicesByGarageBusinessId(1).Data.Count);
        Assert.IsFalse(service.AddOrUpdateVehicleInvoice(Invoice(), 1, "actor").Success);
        Assert.IsFalse(service.MarkInvoiceAsPaid(1, 1, "actor").Success); Unchanged(db, before);
    }

    [TestMethod]
    public void DirectFallbackMultipleQuotesAndNullHistoryRemainSupportedWithoutTrackedReadChanges()
    {
        using var db = Database(); db.VehicleInvoice.Single().WorkQuoteId = 100; db.SaveChanges();
        var service = new VehicleInvoiceService(db); Assert.AreEqual(1, service.GetVehicleInvoice(1, 1).Data.WorkItems.Count);
        db.InvoiceWorkQuote.AddRange(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 100, GarageBusinessCustomerId = 1 },
            new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 101, GarageBusinessCustomerId = 1 },
            new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = null, GarageBusinessCustomerId = 1 });
        db.SaveChanges(); var before = State(db);
        foreach (var entry in db.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
        var details = service.GetVehicleInvoice(1, 1);
        Assert.IsTrue(details.Success); Assert.AreEqual(2, details.Data.WorkItems.Count);
        Assert.AreEqual(123.5m, details.Data.Invoice.Total); Unchanged(db, before);
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
        var edit = Invoice(); edit.WorkQuoteId = 100; edit.Comment = "Ordinary edit";
        Assert.IsTrue(service.AddOrUpdateVehicleInvoice(edit, 1, "actor").Success);
        Assert.AreEqual(3, db.InvoiceWorkQuote.Count());
    }

    [DataTestMethod]
    [DataRow("customer")][DataRow("vehicle")][DataRow("quote")]
    public void LinkedInvoicesRejectRetargetingButNotOrdinaryEdits(string field)
    {
        using var db = Database(); db.VehicleInvoice.Single().WorkQuoteId = 100;
        db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 100, GarageBusinessCustomerId = 1 }); db.SaveChanges();
        var before = State(db); var edit = Invoice(); edit.WorkQuoteId = 100;
        if (field == "customer") { edit.CustomerId = 102; edit.GarageBusinessCustomerId = 102; }
        if (field == "vehicle") edit.VehicleId = 11;
        if (field == "quote") edit.WorkQuoteId = 101;
        var service = new VehicleInvoiceService(db);
        Assert.IsFalse(service.AddOrUpdateVehicleInvoice(edit, 1, "actor").Success); Unchanged(db, before);
        edit = Invoice(); edit.WorkQuoteId = 100; edit.Labour = 250; edit.Comment = "Edit";
        Assert.IsTrue(service.AddOrUpdateVehicleInvoice(edit, 1, "actor").Success);
        Assert.AreEqual(250m, db.VehicleInvoice.Single().Labour);
    }

    [TestMethod]
    public void NullHistoryAndGarageOwnedVehiclesDoNotRequireCurrentOwnershipOrActivity()
    {
        using var db = Database(false); var service = new VehicleInvoiceService(db);
        var history = Invoice(0); history.CustomerId = null; history.GarageBusinessCustomerId = null; history.VehicleId = null;
        Assert.IsTrue(service.AddOrUpdateVehicleInvoice(history, 1, "actor").Success);
        var garageOwned = Invoice(0); garageOwned.CustomerId = null; garageOwned.GarageBusinessCustomerId = null; garageOwned.VehicleId = 11;
        Assert.IsTrue(service.AddOrUpdateVehicleInvoice(garageOwned, 1, "actor").Success);
        var historicCustomer = Invoice(0); historicCustomer.CustomerId = 102; historicCustomer.GarageBusinessCustomerId = 102;
        db.GarageBusinessCustomer.Single(x => x.Id == 102).Active = false; db.CustomerVehicle.Single(x => x.Id == 10).Active = false; db.SaveChanges();
        Assert.IsTrue(service.AddOrUpdateVehicleInvoice(historicCustomer, 1, "actor").Success);
        Assert.AreEqual(3, service.GetInvoicesByGarageBusinessId(1).Data.Count);
    }

    [TestMethod]
    public void DropdownFiltersOwnershipTenancyAndIncludesGarageOwnedWithoutSyntheticCustomer()
    {
        using var db = Database();
        db.CustomerOwnedVehicles.AddRange(new CustomerOwnedVehicles { GarageBusinessId = 2, VehicleId = 10, GarageBusinessCustomerId = 201 },
            new CustomerOwnedVehicles { GarageBusinessId = 1, VehicleId = 10, GarageBusinessCustomerId = 201 },
            new CustomerOwnedVehicles { GarageBusinessId = 1, VehicleId = 20, GarageBusinessCustomerId = 101 }); db.SaveChanges();
        var service = new VehicleInvoiceService(db); var options = service.GetVehicleDropdownItemsForGarageBusiness(1);
        Assert.AreEqual(2, options.Count); Assert.IsFalse(options.Any(x => x.Id == 20 || x.GarageBusinessCustomerId == 201));
        Assert.IsNull(options.Single(x => x.Id == 11).GarageBusinessCustomerId);
    }

    [DataTestMethod]
    [DataRow("customer")][DataRow("vehicle")][DataRow("work-link")][DataRow("work-item")][DataRow("work-customer")][DataRow("work-vehicle")]
    public void ConversionRejectsUnsafeQuoteGraphBeforeAnyWrites(string kind)
    {
        using var db = Database(false);
        if (kind == "customer") db.WorkQuote.Single(x => x.Id == 100).CustomerId = 201;
        if (kind == "vehicle") db.WorkQuote.Single(x => x.Id == 100).VehicleId = 20;
        if (kind == "work-link") db.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == 100).GarageBusinessCustomerId = 2;
        if (kind == "work-item") db.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == 100).WorkItemId = 2000;
        if (kind == "work-customer") db.WorkItem.Single(x => x.Id == 1000).CustomerId = 201;
        if (kind == "work-vehicle") db.WorkItem.Single(x => x.Id == 1000).VehicleId = 20;
        db.SaveChanges();
        Assert.IsFalse(new VehicleInvoiceService(db).CreateFromWorkQuote(100, 1, "actor").Success);
        Assert.AreEqual(0, db.VehicleInvoice.Count()); Assert.AreEqual(0, db.InvoiceWorkQuote.Count()); Assert.IsFalse(db.ChangeTracker.HasChanges());
    }

    [TestMethod]
    public void ConversionAndMarkPaidRetainExistingBehavior()
    {
        using var db = Database(false); var service = new VehicleInvoiceService(db);
        var converted = service.CreateFromWorkQuote(100, 1, "actor"); Assert.IsTrue(converted.Success, converted.ErrorMessage);
        Assert.AreEqual(101, converted.Data.CustomerId); Assert.AreEqual(101, converted.Data.GarageBusinessCustomerId);
        Assert.AreEqual(1, db.InvoiceWorkQuote.Count()); Assert.IsFalse(service.CreateFromWorkQuote(100, 1, "actor").Success);
        Assert.IsTrue(service.MarkInvoiceAsPaid(converted.Data.Id, 1, "actor").Success);
        Assert.AreEqual("Paid", converted.Data.InvoiceStatus); Assert.IsTrue(converted.Data.Paid); Assert.IsNotNull(converted.Data.PaidDate);
        Assert.IsFalse(service.MarkInvoiceAsPaid(converted.Data.Id, 1, "actor").Success);
    }

    [DataTestMethod]
    [DataRow(0)][DataRow(-1)][DataRow(999)]
    public void InvalidGarageRejectedByAllDatabaseMethods(int garage)
    {
        using var db = Database(); var service = new VehicleInvoiceService(db); var before = State(db);
        Assert.IsFalse(service.GetVehicleInvoice(1, garage).Success); Assert.IsFalse(service.GetInvoicesByGarageBusinessId(garage).Success);
        Assert.IsFalse(service.GetInvoicesByGarageCustomerId(garage, 101).Success); Assert.IsFalse(service.AddOrUpdateVehicleInvoice(Invoice(), garage, "actor").Success);
        Assert.IsFalse(service.DeleteVehicleInvoice(1, garage).Success); Assert.IsFalse(service.MarkInvoiceAsPaid(1, garage, "actor").Success);
        Assert.IsFalse(service.CreateFromWorkQuote(100, garage, "actor").Success);
        Assert.AreEqual(0, service.GetCustomersForGarageBusiness(garage).Count); Assert.AreEqual(0, service.GetVehiclesForGarageBusiness(garage).Count);
        Assert.AreEqual(0, service.GetVehicleDropdownItemsForGarageBusiness(garage).Count); Unchanged(db, before);
    }
}

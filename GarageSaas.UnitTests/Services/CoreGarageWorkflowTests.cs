using GarageSaas.Services;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class CoreGarageWorkflowTests
{
    internal static VehicleServiceHistoryService History(SignupContext db) =>
        new(db, new WorkItemService(db), new VehicleInvoiceService(db));

    [TestMethod]
    public void CustomerVehicleWorkQuoteInvoicePaymentHistoryPersistAcrossContexts()
    {
        var options = VehicleInvoiceServiceTests.Options();
        using var db = new SignupContext(options);
        db.GarageBusiness.AddRange(new GarageBusiness { GarageBusinessName = "Test garage",  Id = 1, Active = true }, new GarageBusiness { GarageBusinessName = "Test garage",  Id = 2, Active = true });
        db.SaveChanges();
        var customer = new GarageBusinessCustomer { GarageCustomerForename = "Pat", GarageCustomerSurname = "Driver" };
        var customers = new GarageCustomersService(db, null);
        Assert.IsTrue(customers.AddOrUpdateGarageCustomer(customer, 1, "mechanic").Success);
        var customerId = db.GarageBusinessCustomer.Single().Id;
        var vm = new VehicleAndCustomers { Vehicle = new CustomerVehicle { GarageOwned = false,  VehicleRegistration = "261-D-123", VehicleMileageDate = DateTime.Today },
            GarageVehicleOwnerListItem = new SelectListItem { Value = customerId.ToString() } };
        Assert.IsTrue(new CustomerVehicleService(db, null).AddOrUpdateCustomerVehicle(vm, 1, "mechanic").Success);
        var vehicleId = db.CustomerVehicle.Single().Id;
        Assert.AreEqual(customerId, db.CustomerOwnedVehicles.Single().GarageBusinessCustomerId);
        var work = new WorkItemService(db).AddOrUpdateWorkItem(new WorkItem { VehicleId = vehicleId, CustomerId = customerId, RepairInstructions = "Replace brake pads" }, 1, "mechanic");
        Assert.IsTrue(work.Success, work.ErrorMessage);
        Assert.AreEqual(0, History(db).Get(vehicleId, 1).Data.Entries.Count);
        var quotes = new WorkQuoteService(db);
        Assert.IsTrue(quotes.GetVehiclesForGarageCustomer(customerId, 1).Data.Any(v => v.Id == vehicleId), "Vehicle with no make/model must remain selectable.");
        var quote = quotes.AddOrUpdateWorkQuote(new CombinedWorkQuoteWorkitem { CustomerId = customerId, VehicleId = vehicleId,
            WorkQuoteDate = DateTime.Today, WorkItemIds = new List<int> { work.Data.Id }, Labour = 100m }, 1, "mechanic");
        Assert.IsTrue(quote.Success, quote.ErrorMessage);
        var invoices = new VehicleInvoiceService(db);
        var converted = invoices.CreateFromWorkQuote(quote.Data.WorkQuoteId, 1, "mechanic");
        Assert.IsTrue(converted.Success, converted.ErrorMessage);
        Assert.AreEqual($"INV-{DateTime.Now:yyyy}-{converted.Data.Id:D6}", converted.Data.InvoiceNumber);
        Assert.AreEqual(123.50m, converted.Data.Total);
        Assert.IsFalse(invoices.CreateFromWorkQuote(quote.Data.WorkQuoteId, 1, "mechanic").Success);
        Assert.IsTrue(invoices.MarkInvoiceAsPaid(converted.Data.Id, 1, "mechanic").Success);
        Assert.IsFalse(invoices.MarkInvoiceAsPaid(converted.Data.Id, 2, "other").Success);

        using var fresh = new SignupContext(options);
        var saved = fresh.VehicleInvoice.Single();
        Assert.AreEqual("Paid", saved.InvoiceStatus); Assert.IsTrue(saved.Paid == true); Assert.IsNotNull(saved.PaidDate);
        var history = History(fresh).Get(vehicleId, 1);
        Assert.IsTrue(history.Success); Assert.AreEqual(1, history.Data.Entries.Count);
        Assert.AreEqual("Replace brake pads", history.Data.Entries.Single().WorkItems.Single().RepairInstructions);
        Assert.IsFalse(History(fresh).Get(vehicleId, 2).Success);
        Assert.IsFalse(fresh.ChangeTracker.HasChanges());
    }

    [TestMethod]
    public void HistoryOrdersIssuedInvoicesExcludesDraftCancelledAndForeignGraphsAndLabelsMileage()
    {
        using var db = new SignupContext(VehicleInvoiceServiceTests.Options());
        VehicleInvoiceServiceTests.Seed(db);
        db.Mileage.Add(new Mileage { Id = 1, Kilometers = "65000" });
        db.CustomerVehicle.Single(v => v.Id == 10).VehicleMileageId = 1;
        foreach (var pair in new[] { (1, "Pending"), (2, "Paid"), (3, "Draft"), (4, "Cancelled") })
        {
            var invoice = VehicleInvoiceServiceTests.Invoice(pair.Item1);
            invoice.InvoiceStatus = pair.Item2; invoice.InvoiceDate = new DateTime(2026, 1, pair.Item1); invoice.WorkQuoteId = 100;
            db.VehicleInvoice.Add(invoice);
        }
        var unsafeInvoice = VehicleInvoiceServiceTests.Invoice(5); unsafeInvoice.CustomerId = 201; db.VehicleInvoice.Add(unsafeInvoice);
        db.SaveChanges();
        var result = History(db).Get(10, 1);
        Assert.IsTrue(result.Success); Assert.AreEqual("65000", result.Data.LatestMileage);
        CollectionAssert.AreEqual(new[] { 2, 1 }, result.Data.Entries.Select(e => e.InvoiceId).ToArray());
        Assert.AreEqual(1000, result.Data.Entries[0].WorkItems.Single().Id);
        Assert.IsFalse(History(db).Get(20, 1).Success);
        Assert.IsFalse(History(db).Get(999, 1).Success);
        Assert.IsFalse(db.ChangeTracker.HasChanges());
    }

    [TestMethod]
    public void HistoryIncludesManualInvoicesAndOnlyTargetVehicleWorkFromMultiVehicleQuotes()
    {
        using var db = new SignupContext(VehicleInvoiceServiceTests.Options()); VehicleInvoiceServiceTests.Seed(db);
        db.WorkItem.Add(new WorkItem { Id = 1002, GarageBusinessCustomerId = 1, VehicleId = 11, RepairInstructions = "Garage car repair" });
        db.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem { GarageBusinessCustomerId = 1, WorkQuoteId = 100, WorkItemId = 1002 });
        var linked = VehicleInvoiceServiceTests.Invoice(); linked.WorkQuoteId = 100; db.VehicleInvoice.Add(linked);
        var manual = VehicleInvoiceServiceTests.Invoice(2); manual.InvoiceDescription = "Oil change"; db.VehicleInvoice.Add(manual); db.SaveChanges();
        var target = History(db).Get(10, 1).Data;
        Assert.AreEqual(2, target.Entries.Count); Assert.AreEqual("Oil change", target.Entries.Single(e => e.InvoiceId == 2).Description);
        Assert.IsFalse(target.Entries.SelectMany(e => e.WorkItems).Any(w => w.VehicleId != 10));
        var garageCar = History(db).Get(11, 1).Data;
        Assert.AreEqual(1, garageCar.Entries.Count); Assert.AreEqual("Garage car repair", garageCar.Entries[0].WorkItems.Single().RepairInstructions);
        Assert.IsNull(garageCar.Entries[0].Description);
        Assert.IsTrue(new WorkQuoteService(db).GetVehiclesForGarageCustomer(101, 1).Data.Any(v => v.Id == 11));
    }
}



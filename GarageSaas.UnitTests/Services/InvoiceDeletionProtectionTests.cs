using System.Text.Json;
using GarageSaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class InvoiceDeletionProtectionTests
{
    public static IEnumerable<object[]> Associations() =>
        from kind in new[] { "join", "direct", "both", "multi", "foreign-label", "null-label", "missing", "null-endpoint", "zero-direct", "negative-direct", "missing-direct", "foreign-direct", "foreign-endpoint" }
        from status in new[] { "Pending", "Paid", "Cancelled" }
        select new object[] { kind, status };

    [DataTestMethod, DynamicData(nameof(Associations), DynamicDataSourceType.Method)]
    public void EveryAssociationBlocksDeletionWithoutChangingAnyData(string kind, string status)
    {
        using var db = new SignupContext(VehicleInvoiceServiceTests.Options()); VehicleInvoiceServiceTests.Seed(db);
        var invoice = VehicleInvoiceServiceTests.Invoice(); invoice.InvoiceStatus = status; invoice.Paid = status == "Paid";
        if (kind is "direct" or "both") invoice.WorkQuoteId = 100;
        if (kind == "zero-direct") invoice.WorkQuoteId = 0;
        if (kind == "negative-direct") invoice.WorkQuoteId = -1;
        if (kind == "missing-direct") invoice.WorkQuoteId = 99999;
        if (kind == "foreign-direct") invoice.WorkQuoteId = 200;
        if (kind is not ("direct" or "zero-direct" or "negative-direct" or "missing-direct" or "foreign-direct"))
            db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 1,
                WorkQuoteId = kind == "missing" ? 99999 : kind == "null-endpoint" ? null : kind == "foreign-endpoint" ? 200 : 100,
                GarageBusinessCustomerId = kind == "foreign-label" ? 2 : kind == "null-label" ? null : 1 });
        if (kind == "multi") db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 101, GarageBusinessCustomerId = 1 });
        db.VehicleInvoice.Add(invoice); db.SaveChanges(); var before = State(db);
        var result = new VehicleInvoiceService(db).DeleteVehicleInvoice(1, 1);
        Assert.IsFalse(result.Success);
        Assert.AreEqual("This invoice cannot be deleted because it is linked to one or more work quotes.", result.ErrorMessage);
        Assert.AreEqual(before, State(db)); Assert.IsFalse(db.ChangeTracker.HasChanges());
        db.SaveChanges(); Assert.AreEqual(before, State(db));
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void UnlinkedInvoiceDeletionPreservesExistingPaidAndUnpaidBehavior(bool paid)
    {
        using var db = new SignupContext(VehicleInvoiceServiceTests.Options()); VehicleInvoiceServiceTests.Seed(db);
        var invoice = VehicleInvoiceServiceTests.Invoice(); invoice.Paid = paid; invoice.InvoiceStatus = paid ? "Paid" : "Pending";
        db.VehicleInvoice.Add(invoice); db.SaveChanges();
        Assert.IsTrue(new VehicleInvoiceService(db).DeleteVehicleInvoice(1, 1).Success);
        Assert.IsFalse(db.VehicleInvoice.Any(x => x.Id == 1));
    }

    [TestMethod]
    public void ForeignAndMissingTargetsAreDeniedAndStaleTrackedDirectReferenceCannotBypassGuard()
    {
        var options = VehicleInvoiceServiceTests.Options();
        using var db = new SignupContext(options); VehicleInvoiceServiceTests.Seed(db);
        db.VehicleInvoice.Add(VehicleInvoiceServiceTests.Invoice()); db.SaveChanges();
        var service = new VehicleInvoiceService(db); var before = State(db);
        Assert.IsFalse(service.DeleteVehicleInvoice(1, 2).Success); Assert.IsFalse(service.DeleteVehicleInvoice(999, 1).Success);
        Assert.AreEqual(before, State(db));
        using (var other = new SignupContext(options)) { other.VehicleInvoice.Single(x => x.Id == 1).WorkQuoteId = 100; other.SaveChanges(); }
        Assert.IsFalse(service.DeleteVehicleInvoice(1, 1).Success);
        Assert.IsTrue(db.VehicleInvoice.AsNoTracking().Any(x => x.Id == 1));
    }

    private static string State(SignupContext db) => JsonSerializer.Serialize(new
    {
        invoices = db.VehicleInvoice.AsNoTracking().OrderBy(x => x.Id).ToList(),
        links = db.InvoiceWorkQuote.AsNoTracking().OrderBy(x => x.Id).ToList(),
        quotes = db.WorkQuote.AsNoTracking().OrderBy(x => x.Id).ToList()
    });
}

using System.Text.Json;
using GarageSaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class ManualQuoteAttachmentTests
{
    private static SignupContext Database()
    {
        var db = new SignupContext(VehicleInvoiceServiceTests.Options()); VehicleInvoiceServiceTests.Seed(db); return db;
    }
    private static VehicleInvoice Input(int? quoteId = 100)
    {
        var input = VehicleInvoiceServiceTests.Invoice(0); input.WorkQuoteId = quoteId; return input;
    }
    [DataTestMethod]
    [DataRow(null)] [DataRow(100)] [DataRow(101)]
    public void UnassociatedAndNullableHistoricalQuotesKeepDirectOnlyStorageAndNumberFormat(int? quoteId)
    {
        using var db = Database(); var input = Input(quoteId);
        // Existing tenant rules permit customer/vehicle differences from the quote.
        input.CustomerId = input.GarageBusinessCustomerId = 102; input.VehicleId = 11;
        var service = new VehicleInvoiceService(db); var result = service.AddOrUpdateVehicleInvoice(input, 1, "actor");
        Assert.IsTrue(result.Success, result.ErrorMessage); Assert.AreEqual($"INV-2020-{result.Data.Id:D6}", result.Data.InvoiceNumber);
        Assert.AreEqual(0, input.Id); Assert.AreEqual(quoteId, result.Data.WorkQuoteId); Assert.AreEqual(0, db.InvoiceWorkQuote.Count());
        Assert.IsTrue(service.GetVehicleInvoice(result.Data.Id, 1).Success);
        if (quoteId.HasValue)
            Assert.AreEqual("This work quote has already been converted to an invoice.", service.CreateFromWorkQuote(quoteId.Value, 1, "actor").ErrorMessage);
    }

    [DataTestMethod]
    [DataRow("direct")] [DataRow("join")] [DataRow("mixed")] [DataRow("multi-quote")]
    [DataRow("null-invoice")] [DataRow("missing-invoice")] [DataRow("foreign-invoice")]
    [DataRow("foreign-label")] [DataRow("null-label")] [DataRow("conflict")]
    [DataRow("foreign-direct")] [DataRow("bad-invoice-graph")]
    public void ExistingAndMalformedAssociationsRejectWithoutInvoiceNumberOrAnyMutation(string kind)
    {
        using var db = Database(); var invoice = VehicleInvoiceServiceTests.Invoice();
        if (kind is "direct" or "mixed" or "conflict" or "foreign-direct") invoice.WorkQuoteId = 100;
        if (kind is "foreign-invoice" or "foreign-direct") invoice.GarageBusinessId = 2;
        if (kind == "bad-invoice-graph") invoice.CustomerId = 201;
        db.VehicleInvoice.Add(invoice);
        if (kind is not ("direct" or "foreign-direct"))
            db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { WorkQuoteId = 100,
                InvoiceId = kind == "null-invoice" ? null : kind == "missing-invoice" ? 9999 : 1,
                GarageBusinessCustomerId = kind == "foreign-label" ? 2 : kind == "null-label" ? null : 1 });
        if (kind == "mixed") db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { WorkQuoteId = 100, InvoiceId = 1, GarageBusinessCustomerId = 1 });
        if (kind == "multi-quote") db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { WorkQuoteId = 101, InvoiceId = 1, GarageBusinessCustomerId = 1 });
        if (kind == "conflict") { var second = VehicleInvoiceServiceTests.Invoice(2); second.WorkQuoteId = 100; db.VehicleInvoice.Add(second); }
        db.SaveChanges(); var before = State(db); var input = Input();
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(input, 1, "actor");
        Assert.IsFalse(result.Success); Assert.IsNull(result.Data); Assert.AreEqual(0, input.Id);
        Assert.AreEqual(kind is "direct" or "join" or "mixed" or "multi-quote" ? "This work quote is already linked to an invoice." :
            "Invoice relationships are invalid for this garage.", result.ErrorMessage);
        Assert.AreEqual(before, State(db)); Assert.IsFalse(db.ChangeTracker.HasChanges());
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(-1)] [DataRow(200)] [DataRow(99999)]
    public void InvalidOrForeignQuotesNeverCreateAnInvoice(int id)
    {
        using var db = Database(); var before = State(db);
        var result = new VehicleInvoiceService(db).AddOrUpdateVehicleInvoice(Input(id), 1, "actor");
        Assert.IsFalse(result.Success); Assert.AreEqual("Invoice relationships are invalid for this garage.", result.ErrorMessage);
        Assert.AreEqual(before, State(db));
    }
    private static string State(SignupContext db) => JsonSerializer.Serialize(new
    {
        invoices = db.VehicleInvoice.AsNoTracking().OrderBy(x => x.Id).ToList(),
        links = db.InvoiceWorkQuote.AsNoTracking().OrderBy(x => x.Id).ToList(),
        quotes = db.WorkQuote.AsNoTracking().OrderBy(x => x.Id).ToList()
    });
}

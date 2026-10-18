using System.Text.Json;
using GarageSaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class InvoiceLinkedQuoteProtectionTests
{
    private static SignupContext Database(string association, string status = "Pending")
    {
        var db = WorkQuoteTenantValidationTests.Database();
        var quote = db.WorkQuote.Single(x => x.Id == 100); quote.VehicleId = 11;
        db.WorkItem.Add(new WorkItem { Id = 1002, GarageBusinessCustomerId = 1 });
        // Preserve nullable endpoints and duplicate historical link rows without repair.
        db.WorkQuoteWorkItem.AddRange(
            new WorkQuoteWorkItem { WorkQuoteId = 100, WorkItemId = 1001, GarageBusinessCustomerId = 1 },
            new WorkQuoteWorkItem { WorkQuoteId = 100, WorkItemId = 1000, GarageBusinessCustomerId = 1 },
            new WorkQuoteWorkItem { WorkQuoteId = 100, WorkItemId = null, GarageBusinessCustomerId = 1 });
        var invoice = VehicleInvoiceServiceTests.Invoice(); invoice.VehicleId = 11;
        invoice.WorkQuoteId = association == "join" ? null : 100;
        invoice.InvoiceStatus = status; invoice.Paid = status == "Paid";
        db.VehicleInvoice.Add(invoice);
        if (association != "direct") db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 100, GarageBusinessCustomerId = 1 });
        if (association == "multi") db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 101, GarageBusinessCustomerId = 1 });
        db.SaveChanges();
        foreach (var entry in db.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
        return db;
    }

    private static CombinedWorkQuoteWorkitem Input()
    {
        var input = WorkQuoteTenantValidationTests.Input(100); input.VehicleId = 11;
        input.WorkItemIds = new() { 1000, 1001 }; return input;
    }
    private static string State(SignupContext db) => JsonSerializer.Serialize(new
    {
        quotes = db.WorkQuote.AsNoTracking().OrderBy(x => x.Id).ToList(),
        links = db.WorkQuoteWorkItem.AsNoTracking().OrderBy(x => x.Id).ToList(),
        invoices = db.VehicleInvoice.AsNoTracking().OrderBy(x => x.Id).ToList(),
        associations = db.InvoiceWorkQuote.AsNoTracking().OrderBy(x => x.Id).ToList()
    });

    [DataTestMethod]
    [DataRow("join", "Pending")] [DataRow("direct", "Pending")] [DataRow("multi", "Pending")]
    [DataRow("join", "Paid")] [DataRow("direct", "Paid")] [DataRow("multi", "Paid")]
    [DataRow("join", "Cancelled")] [DataRow("direct", "Cancelled")] [DataRow("multi", "Cancelled")]
    public void LinkedDeletionIsRejectedForEveryInvoiceStatus(string association, string status)
    {
        using var db = Database(association, status); var before = State(db);
        var result = new WorkQuoteService(db).DeleteWorkQuote(100, 1);
        Assert.IsFalse(result.Success); Assert.AreEqual("Invoice-linked work quotes cannot be deleted.", result.ErrorMessage);
        Assert.AreEqual(before, State(db)); Assert.IsFalse(db.ChangeTracker.HasChanges());
        if (association == "multi")
        {
            Assert.IsFalse(new WorkQuoteService(db).DeleteWorkQuote(101, 1).Success);
            Assert.AreEqual(before, State(db));
        }
        db.SaveChanges(); Assert.AreEqual(before, State(db));
    }

    [DataTestMethod]
    [DataRow("join", "customer")] [DataRow("direct", "customer")] [DataRow("multi", "customer")]
    [DataRow("join", "vehicle")] [DataRow("direct", "vehicle")] [DataRow("multi", "vehicle")]
    [DataRow("join", "add")] [DataRow("direct", "add")] [DataRow("multi", "add")]
    [DataRow("join", "remove")] [DataRow("direct", "remove")] [DataRow("multi", "remove")]
    [DataRow("join", "replace")] [DataRow("direct", "replace")] [DataRow("multi", "replace")]
    public void RetargetingIsRejectedBeforeAnyQuoteAuditOrLinkMutation(string association, string change)
    {
        using var db = Database(association); var before = State(db); var input = Input();
        if (change == "customer") input.CustomerId = 102;
        if (change == "vehicle") input.VehicleId = 10;
        if (change == "add") input.WorkItemIds.Add(1002);
        if (change == "remove") input.WorkItemIds.Remove(1000);
        if (change == "replace") input.WorkItemIds = new() { 1001, 1002 };
        input.Comment = "Must not save"; input.Labour = 900;
        var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "attacker");
        Assert.IsFalse(result.Success); Assert.AreEqual("Invoice-linked work quote relationships cannot be changed.", result.ErrorMessage);
        Assert.AreEqual(before, State(db)); Assert.IsFalse(db.ChangeTracker.HasChanges());
        db.SaveChanges(); Assert.AreEqual(before, State(db));
    }

    [DataTestMethod]
    [DataRow("join")] [DataRow("direct")] [DataRow("multi")]
    public void NormalizedSelectionsAllowAmountAndTextEditsWithoutChangingLinksOrInvoice(string association)
    {
        using var db = Database(association);
        var invoices = JsonSerializer.Serialize(db.VehicleInvoice.AsNoTracking().ToList());
        var links = JsonSerializer.Serialize(db.WorkQuoteWorkItem.AsNoTracking().OrderBy(x => x.Id).ToList());
        var associations = JsonSerializer.Serialize(db.InvoiceWorkQuote.AsNoTracking().OrderBy(x => x.Id).ToList());
        var input = Input(); input.WorkItemIds = new() { 1001, 1000, 1001 };
        input.Comment = "New quote note"; input.WorkRequest = "Updated request"; input.VehicleProblem = "Updated problem";
        input.Labour = 333; input.Paint = 4; input.EnvironmentCost = 5; input.SundryExpenses = 6;
        input.CarHire = 7; input.SubTotal = 355; input.Vat = 83; input.Total = 438; input.Tax = 23;
        input.InvoiceNumber = "Quote-only reference"; input.WorkQuoteDate = new DateTime(2026, 3, 4);
        var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "editor");
        Assert.IsTrue(result.Success, result.ErrorMessage); Assert.AreSame(input, result.Data);
        Assert.IsTrue(result.Data.HasInvoice); Assert.AreEqual(1, result.Data.InvoiceId);
        Assert.AreEqual(2, result.Data.WorkItemIds.Count);
        var quote = db.WorkQuote.AsNoTracking().Single(x => x.Id == 100);
        Assert.AreEqual(333m, quote.Labour); Assert.AreEqual(input.Comment, quote.Comment);
        Assert.AreEqual(input.WorkRequest, quote.WorkRequest); Assert.AreEqual(input.VehicleProblem, quote.VehicleProblem);
        Assert.AreEqual(input.WorkQuoteDate, quote.WorkQuoteDate); Assert.AreEqual("editor", quote.UpdatedBy);
        Assert.AreEqual(invoices, JsonSerializer.Serialize(db.VehicleInvoice.AsNoTracking().ToList()));
        Assert.AreEqual(links, JsonSerializer.Serialize(db.WorkQuoteWorkItem.AsNoTracking().OrderBy(x => x.Id).ToList()));
        Assert.AreEqual(associations, JsonSerializer.Serialize(db.InvoiceWorkQuote.AsNoTracking().OrderBy(x => x.Id).ToList()));
        Assert.IsTrue(new VehicleInvoiceService(db).GetVehicleInvoice(1, 1).Success);
        Assert.IsTrue(new VehicleInvoiceService(db).MarkInvoiceAsPaid(1, 1, "cashier").Success);
    }

    [DataTestMethod]
    [DataRow("join")] [DataRow("direct")]
    public void LegacySingleItemInputRemainsValidForLinkedQuote(string association)
    {
        using var db = Database(association);
        db.WorkQuoteWorkItem.RemoveRange(db.WorkQuoteWorkItem.AsNoTracking().Where(x => x.WorkQuoteId == 100 && x.WorkItemId == 1001));
        db.SaveChanges();
        var links = JsonSerializer.Serialize(db.WorkQuoteWorkItem.AsNoTracking().OrderBy(x => x.Id).ToList());
        var input = Input(); input.WorkItemIds.Clear(); input.WorkItemId = 1000;
        var result = new WorkQuoteService(db).AddOrUpdateWorkQuote(input, 1, "editor");
        Assert.IsTrue(result.Success, result.ErrorMessage); Assert.AreEqual(1000, result.Data.WorkItemId);
        Assert.AreEqual(links, JsonSerializer.Serialize(db.WorkQuoteWorkItem.AsNoTracking().OrderBy(x => x.Id).ToList()));
    }
}

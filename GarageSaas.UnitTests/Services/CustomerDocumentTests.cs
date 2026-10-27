using System.Text;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfSharp.Pdf.IO;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class CustomerDocumentTests
{
    internal static CustomerDocumentService Documents(SignupContext db) => new(db, new WorkQuoteService(db), new VehicleInvoiceService(db));
    internal static void Seed(SignupContext db, int garage=1, int customer=101, int vehicle=10, int quote=100, int invoice=1)
    {
        if (!db.GarageBusiness.Any(g => g.Id == garage)) db.Add(new GarageBusiness { GarageBusinessName = "Test garage",  Id=garage, Active=true });
        if (!db.GarageBusinessCustomer.Any(c => c.Id == customer)) db.Add(new GarageBusinessCustomer { Id=customer, GarageBusinessId=garage, Active=true });
        if (!db.CustomerVehicle.Any(v => v.Id == vehicle)) db.Add(new CustomerVehicle { GarageOwned = false,  Id=vehicle, GarageBusinessId=garage });
        db.SaveChanges();
        var g=db.GarageBusiness.Single(x=>x.Id==garage);g.GarageBusinessName="O'Neill Motor Services";g.GarageAddressLine1="12 Main Street";g.GarageAddressLine2="Dublin";g.Postcode="D02 AB12";g.GarageEmailAddress="garage@example.invalid";g.GaragePhoneNumber="01 555 0100";g.VatNumber="IE1234567A";
        var c=db.GarageBusinessCustomer.Single(x=>x.Id==customer);c.GarageCustomerForename="Seán";c.GarageCustomerSurname="Ó Briain";c.GarageCustomerAddressline1="4 Customer Road";c.GarageCustomerEmailAddress="customer@example.invalid";
        var v=db.CustomerVehicle.Single(x=>x.Id==vehicle);v.VehicleRegistration="261-D-123";
        db.Add(new WorkQuote { Id=quote, GarageBusinessCustomerId=garage, CustomerId=customer, VehicleId=vehicle, WorkQuoteDate=new DateTime(2026,10,19), Labour=100, Paint=20, EnvironmentCost=5, SundryExpenses=3, CarHire=0, SubTotal=128, Vat=30.08m, Total=158.08m, Comment="Please contact us to arrange the repair." });
        db.Add(new WorkItem { Id=quote*10, GarageBusinessCustomerId=garage, CustomerId=customer, VehicleId=vehicle, RepairInstructions="Replace front brake pads and inspect discs." });
        db.Add(new WorkQuoteWorkItem { GarageBusinessCustomerId=garage, WorkQuoteId=quote, WorkItemId=quote*10 });
        db.Add(new VehicleInvoice { Id=invoice, GarageBusinessId=garage, CustomerId=customer, GarageBusinessCustomerId=customer, VehicleId=vehicle, WorkQuoteId=quote, InvoiceNumber=$"INV-2026-{invoice:D6}", InvoiceDate=new DateTime(2026,10,19), DateDue=new DateTime(2026,11,2), Labour=100, Paint=20, EnvironmentCost=5, SundryExpenses=3, CarHire=0, InvoiceStatus="Paid", Paid=true });
        db.SaveChanges();
    }

    [DataTestMethod] [DataRow("quote")] [DataRow("invoice")]
    public void DocumentUsesValidatedGarageCustomerVehicleAndExistingAmounts(string kind)
    {
        using var db=new SignupContext(VehicleInvoiceServiceTests.Options());Seed(db);
        var result=Documents(db).Get(kind,kind=="quote"?100:1,1);
        Assert.IsTrue(result.Success);var d=result.Data;
        Assert.AreEqual("O'Neill Motor Services",d.Garage.GarageBusinessName);Assert.AreEqual("Seán",d.Customer.GarageCustomerForename);
        Assert.AreEqual("261-D-123",d.Vehicle.VehicleRegistration);Assert.AreEqual("customer@example.invalid",d.Recipient);
        Assert.AreEqual(128m,d.SubTotal);Assert.AreEqual(30.08m,d.Vat);Assert.AreEqual(158.08m,d.Total);
        Assert.AreEqual("Replace front brake pads and inspect discs.",d.WorkDescriptions.Single());
        Assert.AreEqual(kind=="quote"?"Q-100":"INV-2026-000001",d.Number);
        Assert.IsFalse(db.ChangeTracker.HasChanges());
        var pdf=new CustomerDocumentPdfRenderer().Generate(d);
        Assert.AreEqual("%PDF-",Encoding.ASCII.GetString(pdf,0,5));
        using var document=PdfReader.Open(new MemoryStream(pdf),PdfDocumentOpenMode.Import);
        Assert.IsTrue(document.PageCount>=1);Assert.AreEqual(595.28,document.Pages[0].Width.Point, .1);
        SaveSample(kind+".pdf",pdf);
    }

    [TestMethod]
    public void QuoteRetainsStoredAmountsAndNullsWithoutInventingInvoiceCalculations()
    {
        using var db=new SignupContext(VehicleInvoiceServiceTests.Options());Seed(db);
        var quote=db.WorkQuote.Single();quote.SubTotal=999;quote.Vat=1;quote.Total=1000;db.SaveChanges();
        var document=Documents(db).Get("quote",100,1).Data;Assert.AreEqual(1000m,document.Total);Assert.AreEqual(1m,document.Vat);
        quote.Total=null;quote.Vat=null;quote.SubTotal=null;db.SaveChanges();
        document=Documents(db).Get("quote",100,1).Data;Assert.IsNull(document.Total);Assert.IsNull(document.Vat);
    }

    [DataTestMethod] [DataRow("quote",100,2)] [DataRow("invoice",1,2)] [DataRow("quote",999,1)] [DataRow("invoice",0,1)] [DataRow("invoice",-1,1)] [DataRow("unknown",1,1)]
    public void MissingForeignAndInvalidDocumentsFailClosed(string kind,int id,int garage)
    {
        using var db=new SignupContext(VehicleInvoiceServiceTests.Options());Seed(db);
        Assert.IsFalse(Documents(db).Get(kind,id,garage).Success);
    }

    [TestMethod]
    public void UnsafeRelatedCustomerIsNeverReturned()
    {
        using var db=new SignupContext(VehicleInvoiceServiceTests.Options());Seed(db);
        db.Add(new GarageBusinessCustomer { Id=999,GarageBusinessId=2,GarageCustomerEmailAddress="secret@example.invalid" });
        db.WorkQuote.Single().CustomerId=999;db.SaveChanges();
        Assert.IsFalse(Documents(db).Get("quote",100,1).Success);Assert.IsFalse(Documents(db).Get("invoice",1,1).Success);
    }

    [TestMethod]
    public void LongDescriptionsAndNotesPaginateOnA4()
    {
        using var db=new SignupContext(VehicleInvoiceServiceTests.Options());Seed(db);
        var model=Documents(db).Get("quote",100,1).Data;
        model.WorkDescriptions=Enumerable.Range(1,80).Select(i=>$"Service operation {i}: Inspect vehicle, replace components as required and road test. Customer reports intermittent noise when braking. Check fluid levels and record findings.").ToList();
        model.WorkDescriptions.Add(new string('X',300)+" End of long description.");
        model.Notes="Long final note. "+string.Join(" ",Enumerable.Repeat("Contact the garage to discuss these findings.",60));
        var pdf=new CustomerDocumentPdfRenderer().Generate(model);
        using var document=PdfReader.Open(new MemoryStream(pdf),PdfDocumentOpenMode.Import);
        Assert.IsTrue(document.PageCount>=3);Assert.IsTrue(document.PageCount<20);
        SaveSample("multipage-quote.pdf",pdf);
    }

    [DataTestMethod] [DataRow("")] [DataRow("invalid")] [DataRow("a@localhost")] [DataRow("a@example.com,b@example.com")] [DataRow("A <a@example.com>")] [DataRow("a@example.com\r\nBcc:b@example.com")]
    public void InvalidRecipientsAreRejected(string recipient) => Assert.IsFalse(DocumentSendTokens.ValidRecipient(recipient));

    [TestMethod]
    public void SendTokensBindIdentityGarageDocumentRecipientAndAreSingleUse()
    {
        using var cache=new MemoryCache(new MemoryCacheOptions());var tokens=new DocumentSendTokens(cache);
        var token=tokens.Issue(1,1,"quote",100,"a@example.com");
        Assert.IsFalse(tokens.Consume(token,2,1,"quote",100,"a@example.com"));
        Assert.IsFalse(tokens.Consume(token,1,2,"quote",100,"a@example.com"));
        Assert.IsFalse(tokens.Consume(token,1,1,"invoice",100,"a@example.com"));
        Assert.IsFalse(tokens.Consume(token,1,1,"quote",101,"a@example.com"));
        Assert.IsFalse(tokens.Consume(token,1,1,"quote",100,"b@example.com"));
        Assert.IsTrue(tokens.Consume(token,1,1,"quote",100,"a@example.com"));
        Assert.IsFalse(tokens.Consume(token,1,1,"quote",100,"a@example.com"));
    }
    private static void SaveSample(string name,byte[] bytes)
    {
        var directory=Environment.GetEnvironmentVariable("GARAGESAAS_PDF_SAMPLE_DIRECTORY");
        if(string.IsNullOrEmpty(directory))return;
        Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,name),bytes);
    }
}

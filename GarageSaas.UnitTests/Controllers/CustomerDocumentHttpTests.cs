using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using GarageSaas.UnitTests.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;
using static GarageSaas.UnitTests.Controllers.GarageSwitchingHttpTests;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class CustomerDocumentHttpTests
{
    private static async Task<SwitchHost> Host(Mock<IEmailService>? email=null,ICustomerDocumentPdfRenderer? pdf=null)
    {
        var h=await SwitchHost.Start(email:email?.Object,pdf:pdf);await h.Select(1);
        using var db=new SignupContext(h.Options);CustomerDocumentTests.Seed(db,1,1,10,100,1);
        db.Add(new GarageBusinessCustomer { Id=3,GarageBusinessId=2,GarageCustomerForename="Foreign customer",GarageCustomerEmailAddress="secret@example.invalid" });
        db.Add(new CustomerVehicle { Id=20,GarageBusinessId=2,VehicleRegistration="SECRET" });
        db.Add(new WorkQuote { Id=200,GarageBusinessCustomerId=2,CustomerId=3,VehicleId=20 });
        db.Add(new VehicleInvoice { Id=2,GarageBusinessId=2,CustomerId=3,VehicleId=20 });db.SaveChanges();return h;
    }
    private static async Task<Dictionary<string,string>> Form(SwitchHost h,string kind="quote",int id=100)
    {
        using var page=await h.Send("GET",$"/CustomerDocument/Send?kind={kind}&id={id}");
        Assert.AreEqual(HttpStatusCode.OK,page.StatusCode);var html=await page.Content.ReadAsStringAsync();
        StringAssert.Contains(html,"customer@example.invalid");
        var form=Regex.Match(html,@"<form\b[^>]*id=""sendDocumentForm""[^>]*>([\s\S]*?)</form>").Value;
        Assert.IsFalse(string.IsNullOrEmpty(form));
        var inputs=Regex.Matches(form,@"<input\b[^>]*>").Cast<System.Text.RegularExpressions.Match>()
            .Select(m=>new { Name=Regex.Match(m.Value,"name=\"([^\"]+)\"").Groups[1].Value,
                Value=WebUtility.HtmlDecode(Regex.Match(m.Value,"value=\"([^\"]*)\"").Groups[1].Value) }).ToList();
        Assert.AreEqual(1,inputs.Count(i=>i.Name=="__GarageContext"));Assert.AreEqual(1,inputs.Count(i=>i.Name=="__RequestVerificationToken"));
        return inputs.ToDictionary(i=>i.Name,i=>i.Value);
    }
    [DataTestMethod] [DataRow("quote",100)] [DataRow("invoice",1)]
    public async Task DownloadReturnsPdfAndConfirmationSendsAttachmentOnce(string kind,int id)
    {
        var email=new Mock<IEmailService>();await using var h=await Host(email);
        using var download=await h.Send("GET",$"/CustomerDocument/Download?kind={kind}&id={id}");
        Assert.AreEqual(HttpStatusCode.OK,download.StatusCode);Assert.AreEqual("application/pdf",download.Content.Headers.ContentType!.MediaType);
        Assert.IsTrue(download.Headers.CacheControl!.NoStore);StringAssert.EndsWith(download.Content.Headers.ContentDisposition!.FileNameStar!,".pdf");
        var bytes=await download.Content.ReadAsByteArrayAsync();Assert.AreEqual("%PDF-",Encoding.ASCII.GetString(bytes,0,5));
        var fields=await Form(h,kind,id);fields["recipientEmail"]="attacker@example.invalid";
        using var sent=await h.Send("POST","/CustomerDocument/Send",fields,antiforgery:"missing");Assert.AreEqual(HttpStatusCode.Redirect,sent.StatusCode);
        email.Verify(e=>e.SendDocumentAsync("customer@example.invalid",It.Is<string>(s=>s.StartsWith(kind=="quote"?"Quote":"Invoice")),It.IsAny<string>(),It.Is<string>(s=>s.EndsWith(".pdf")),It.Is<byte[]>(b=>b.Length>1000)),Times.Once);
        using var feedback=await h.Send("GET",sent.Headers.Location!.OriginalString);
        StringAssert.Contains(await feedback.Content.ReadAsStringAsync(),"sent to customer@example.invalid");
        using var duplicate=await h.Send("POST","/CustomerDocument/Send",fields,antiforgery:"missing");Assert.AreEqual(HttpStatusCode.Redirect,duplicate.StatusCode);
        email.Verify(e=>e.SendDocumentAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<byte[]>()),Times.Once);
    }
    [DataTestMethod] [DataRow("quote",200)] [DataRow("invoice",2)] [DataRow("quote",999)] [DataRow("invoice",0)] [DataRow("bad",1)]
    public async Task MissingAndForeignDocumentsNeverGenerateOrSend(string kind,int id)
    {
        var email=new Mock<IEmailService>();var pdf=new Mock<ICustomerDocumentPdfRenderer>();await using var h=await Host(email,pdf.Object);
        foreach(var action in new[]{"Download","Send"})
        {using var response=await h.Send("GET",$"/CustomerDocument/{action}?kind={kind}&id={id}");Assert.AreEqual(HttpStatusCode.NotFound,response.StatusCode);}
        var fields=await Form(h);fields["kind"]=kind;fields["id"]=id.ToString();
        using var post=await h.Send("POST","/CustomerDocument/Send",fields,antiforgery:"missing");Assert.AreEqual(HttpStatusCode.NotFound,post.StatusCode);
        pdf.Verify(p=>p.Generate(It.IsAny<CustomerDocumentModel>()),Times.Never);email.VerifyNoOtherCalls();
    }
    [TestMethod]
    public async Task GenerationFailureNeverEmailsAndProviderFailureIsReportedWithoutRetry()
    {
        var email=new Mock<IEmailService>();var pdf=new Mock<ICustomerDocumentPdfRenderer>();pdf.Setup(p=>p.Generate(It.IsAny<CustomerDocumentModel>())).Throws(new InvalidOperationException("Renderer failed"));
        await using var h=await Host(email,pdf.Object);var fields=await Form(h);
        using var failed=await h.Send("POST","/CustomerDocument/Send",fields,antiforgery:"missing");Assert.AreEqual(HttpStatusCode.Redirect,failed.StatusCode);email.VerifyNoOtherCalls();
        using var failedFeedback=await h.Send("GET",failed.Headers.Location!.OriginalString);
        StringAssert.Contains(await failedFeedback.Content.ReadAsStringAsync(),"No email was sent");
        using var failedDownload=await h.Send("GET","/CustomerDocument/Download?kind=quote&id=100");Assert.AreEqual(HttpStatusCode.Redirect,failedDownload.StatusCode);
        pdf.Setup(p=>p.Generate(It.IsAny<CustomerDocumentModel>())).Returns(Encoding.ASCII.GetBytes("%PDF-test"));
        email.Setup(e=>e.SendDocumentAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<byte[]>())).ThrowsAsync(new IOException("SMTP failure"));
        fields=await Form(h);using var provider=await h.Send("POST","/CustomerDocument/Send",fields,antiforgery:"missing");Assert.AreEqual(HttpStatusCode.Redirect,provider.StatusCode);
        using var providerFeedback=await h.Send("GET",provider.Headers.Location!.OriginalString);
        StringAssert.Contains(await providerFeedback.Content.ReadAsStringAsync(),"Email delivery could not be confirmed");
        using var again=await h.Send("POST","/CustomerDocument/Send",fields,antiforgery:"missing");
        email.Verify(e=>e.SendDocumentAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<byte[]>()),Times.Once);
    }
    [TestMethod]
    public async Task InvalidAndChangedRecipientCannotSend()
    {
        var email=new Mock<IEmailService>();await using var h=await Host(email);var fields=await Form(h);
        using(var db=new SignupContext(h.Options)){db.GarageBusinessCustomer.Single(c=>c.Id==1).GarageCustomerEmailAddress="changed@example.invalid";db.SaveChanges();}
        using var changed=await h.Send("POST","/CustomerDocument/Send",fields,antiforgery:"missing");Assert.AreEqual(HttpStatusCode.Redirect,changed.StatusCode);
        using(var db=new SignupContext(h.Options)){db.GarageBusinessCustomer.Single(c=>c.Id==1).GarageCustomerEmailAddress="invalid address";db.SaveChanges();}
        using var page=await h.Send("GET","/CustomerDocument/Send?kind=quote&id=100");var html=await page.Content.ReadAsStringAsync();StringAssert.Contains(html,"no valid email address");Assert.IsFalse(html.Contains("id=\"sendDocumentForm\""));
        using var invalid=await h.Send("POST","/CustomerDocument/Send",fields,antiforgery:"missing");Assert.AreEqual(HttpStatusCode.Redirect,invalid.StatusCode);email.VerifyNoOtherCalls();
    }
    [TestMethod]
    public async Task AntiforgeryAndGarageSwitchingProtectSending()
    {
        var email=new Mock<IEmailService>();await using var h=await Host(email);var fields=await Form(h);
        var noAnti=new Dictionary<string,string>(fields);noAnti.Remove("__RequestVerificationToken");
        using var invalid=await h.Send("POST","/CustomerDocument/Send",noAnti,antiforgery:"missing");Assert.AreEqual(HttpStatusCode.BadRequest,invalid.StatusCode);
        await h.Select(2);
        using var stale=await h.Send("POST","/CustomerDocument/Send",fields,antiforgery:"missing");Assert.AreEqual(HttpStatusCode.Conflict,stale.StatusCode);
        using var hidden=await h.Send("GET","/CustomerDocument/Download?kind=quote&id=100");Assert.AreEqual(HttpStatusCode.NotFound,hidden.StatusCode);email.VerifyNoOtherCalls();
    }
    [DataTestMethod] [DataRow("Download")] [DataRow("Send")]
    public async Task AuthenticationAndEligibleGarageAreRequired(string action)
    {
        await using var h=await SwitchHost.Start();
        using var anonymous=await h.Send("GET",$"/CustomerDocument/{action}?kind=quote&id=100",identity:null);Assert.AreEqual(HttpStatusCode.Unauthorized,anonymous.StatusCode);
        using var unselected=await h.Send("GET",$"/CustomerDocument/{action}?kind=quote&id=100");Assert.AreEqual(HttpStatusCode.Forbidden,unselected.StatusCode);
        using var platform=await h.Send("GET",$"/CustomerDocument/{action}?kind=quote&id=100",identity:"platform");Assert.AreEqual(HttpStatusCode.Forbidden,platform.StatusCode);
    }
}

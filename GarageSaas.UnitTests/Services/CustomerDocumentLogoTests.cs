using System.Text;
using GarageSaas.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.Extensions.Configuration;
using PdfSharp.Pdf.IO;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class CustomerDocumentLogoTests
{
    [DataTestMethod]
    [DataRow("quote", "png", false)]
    [DataRow("invoice", "jpg", false)]
    [DataRow("quote", "png", true)]
    [DataRow("invoice", "jpg", true)]
    public void ExistingGarageLogoIsEmbeddedAndDocumentRemainsReadable(string kind, string extension, bool persistent)
    {
        using var db = new SignupContext(VehicleInvoiceServiceTests.Options());
        CustomerDocumentTests.Seed(db);
        var model = CustomerDocumentTests.Documents(db).Get(kind, kind == "quote" ? 100 : 1, 1).Data;
        var root = Path.Combine(Path.GetTempPath(), "GarageSaas-logo-" + Guid.NewGuid().ToString("N"));
        var uploads = persistent ? Path.Combine(root, "persistent-logos") : Path.Combine(root, "uploads", "garage-logos");
        Directory.CreateDirectory(uploads);
        var filename = $"garage-1-{Guid.NewGuid():N}.{extension}";
        var file = Path.Combine(uploads, filename);
        try
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "TestHelpers", "Fixtures", "GarageLogo." + extension), file);
            model.Garage.LogoImage = "/uploads/garage-logos/" + filename;
            var environment = new Mock<IWebHostEnvironment>(); environment.SetupGet(e => e.WebRootPath).Returns(root);
            var settings = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:GarageLogoDirectory"] = persistent ? uploads : null }).Build();
            var pdf = new CustomerDocumentPdfRenderer(environment.Object, settings).Generate(model);
            using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
            Assert.IsTrue(document.Pages[0].Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/XObject")!.Elements.Count > 0, "Logo must be embedded as a PDF image.");
            Assert.AreEqual(1, document.PageCount, "A small logo should preserve the standard one-page layout.");
            var sampleDirectory = Environment.GetEnvironmentVariable("GARAGESAAS_PDF_SAMPLE_DIRECTORY");
            if (!string.IsNullOrEmpty(sampleDirectory))
            { Directory.CreateDirectory(sampleDirectory); File.WriteAllBytes(Path.Combine(sampleDirectory, kind + "-with-logo.pdf"), pdf); }
        }
        finally { Directory.Delete(root, true); }
    }

    [DataTestMethod]
    [DataRow("missing")]
    [DataRow("corrupt")]
    [DataRow("foreign")]
    [DataRow("traversal")]
    [DataRow("remote")]
    public void MissingInvalidOrForeignLogoDoesNotBlockOrReadOtherImages(string scenario)
    {
        using var db = new SignupContext(VehicleInvoiceServiceTests.Options()); CustomerDocumentTests.Seed(db);
        var model = CustomerDocumentTests.Documents(db).Get("quote", 100, 1).Data;
        var root = Path.Combine(Path.GetTempPath(), "GarageSaas-logo-" + Guid.NewGuid().ToString("N"));
        var uploads = Path.Combine(root, "uploads", "garage-logos"); Directory.CreateDirectory(uploads);
        var filename = $"garage-{(scenario == "foreign" ? 2 : 1)}-{Guid.NewGuid():N}.png";
        var file = Path.Combine(uploads, filename);
        try
        {
            if (scenario == "corrupt") File.WriteAllText(file, "not an image");
            if (scenario == "foreign") File.Copy(Path.Combine(AppContext.BaseDirectory, "TestHelpers", "Fixtures", "GarageLogo.png"), file);
            model.Garage.LogoImage = scenario switch
            {
                "remote" => "https://example.invalid/logo.png",
                "traversal" => "/uploads/garage-logos/../../secret.png",
                _ => "/uploads/garage-logos/" + filename
            };
            var environment = new Mock<IWebHostEnvironment>(); environment.SetupGet(e => e.WebRootPath).Returns(root);
            var pdf = new CustomerDocumentPdfRenderer(environment.Object).Generate(model);
            Assert.AreEqual("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
            using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
            Assert.IsNull(document.Pages[0].Elements.GetDictionary("/Resources").Elements.GetDictionary("/XObject"));
        }
        finally { File.Delete(file); Directory.Delete(uploads); Directory.Delete(Path.Combine(root, "uploads")); Directory.Delete(root); }
    }
}

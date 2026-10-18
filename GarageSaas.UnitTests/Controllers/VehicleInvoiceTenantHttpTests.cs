using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using GarageSaas.Controllers;
using GarageSaas.Middleware;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.UnitTests.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class VehicleInvoiceTenantHttpTests
{
    private static IHost _host = null!;
    private static HttpClient _client = null!;
    private static DbContextOptions<SignupContext> _options = null!;

    [ClassInitialize]
    public static async Task Initialize(TestContext _)
    {
        _options = VehicleInvoiceServiceTests.Options();
        using (var db = new SignupContext(_options))
        {
            VehicleInvoiceServiceTests.Seed(db);
            db.Users.AddRange(new Users { Id = 1, ExternalUserId = "member", FirstName = "Member", LastName = "Test", Active = true },
                new Users { Id = 2, ExternalUserId = "platform", FirstName = "Platform", LastName = "Test", Active = true, IsPlatformAdministrator = true },
                new Users { Id = 3, ExternalUserId = "inactive", FirstName = "Inactive", LastName = "Test", Active = true },
                new Users { Id = 4, ExternalUserId = "blocked", FirstName = "Blocked", LastName = "Test", Active = true, Blocked = true });
            db.GarageBusinessUser.AddRange(new GarageBusinessUser { UserId = 1, GarageBusinessId = 1, IsActive = true, Role = "Staff" },
                new GarageBusinessUser { UserId = 3, GarageBusinessId = 1, IsActive = false, Role = "Staff" },
                new GarageBusinessUser { UserId = 4, GarageBusinessId = 1, IsActive = true, Role = "Staff" });
            var unsafeCustomer = VehicleInvoiceServiceTests.Invoice(2); unsafeCustomer.CustomerId = 201; unsafeCustomer.GarageBusinessCustomerId = 201;
            var foreign = VehicleInvoiceServiceTests.Invoice(3); foreign.GarageBusinessId = 2; foreign.CustomerId = 201; foreign.GarageBusinessCustomerId = 201; foreign.VehicleId = 20;
            var unsafeQuote = VehicleInvoiceServiceTests.Invoice(4); unsafeQuote.WorkQuoteId = 200;
            db.VehicleInvoice.AddRange(VehicleInvoiceServiceTests.Invoice(), unsafeCustomer, foreign, unsafeQuote);
            db.SaveChanges();
        }
        _host = await new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseKestrel(server => server.Listen(IPAddress.Loopback, 0));
            web.ConfigureServices(services =>
            {
                services.AddLogging(); services.AddHttpContextAccessor();
                services.AddScoped(_ => new SignupContext(_options));
                services.AddScoped<ICurrentGarageUserService, CurrentGarageUserService>();
                services.AddScoped<IVehicleInvoiceService, VehicleInvoiceService>();
                services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, IdentityHandler>("Test", _ => { });
                services.AddAuthorization();
                services.AddControllersWithViews().AddApplicationPart(typeof(VehicleInvoiceController).Assembly);
            });
            web.Configure(app =>
            {
                app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseMiddleware<GarageAccessMiddleware>();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
                    endpoints.MapGet("/test-token", async context =>
                    {
                        var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
                        await context.Response.WriteAsync(tokens.RequestToken!);
                    });
                });
            });
        }).StartAsync();
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        { BaseAddress = new Uri(_host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }

    [ClassCleanup]
    public static async Task Cleanup() { _client?.Dispose(); if (_host != null) { await _host.StopAsync(); _host.Dispose(); } }

    [DataTestMethod]
    [DataRow("customer", 201)] [DataRow("alias", 201)] [DataRow("vehicle", 20)] [DataRow("quote", 200)]
    [DataRow("customer", 9999)] [DataRow("vehicle", 9999)] [DataRow("quote", 9999)]
    public async Task JsonMutationsRejectForeignAndMissingReferencesWithoutWrites(string field, int id)
    {
        using var db = new SignupContext(_options); var before = JsonSerializer.Serialize(db.VehicleInvoice.AsNoTracking().OrderBy(x => x.Id).ToList());
        var input = VehicleInvoiceServiceTests.Invoice(0);
        if (field == "customer") { input.CustomerId = id; input.GarageBusinessCustomerId = null; }
        if (field == "alias") { input.GarageBusinessCustomerId = id; input.CustomerId = null; }
        if (field == "vehicle") input.VehicleId = id;
        if (field == "quote") input.WorkQuoteId = id;
        using var response = await Send("/VehicleInvoice/AddVehicleInvoiceApi", input);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Error", json.GetProperty("status").GetString());
        Assert.AreEqual(before, JsonSerializer.Serialize(db.VehicleInvoice.AsNoTracking().OrderBy(x => x.Id).ToList()));
    }

    [DataTestMethod]
    [DataRow("/VehicleInvoice/GetApi?invoiceId=2")]
    [DataRow("/VehicleInvoice/GetApi?invoiceId=3")]
    [DataRow("/VehicleInvoice/GetApi?invoiceId=4")]
    [DataRow("/VehicleInvoice/GetApiByGarageBusinessId")]
    [DataRow("/VehicleInvoice/GetApiByGarageCustomerId?garageCustomerId=101")]
    public async Task JsonReadsCannotExposeForeignData(string route)
    {
        using var response = await Get(route); Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(); Assert.IsFalse(text.Contains("SECRET")); Assert.IsFalse(text.Contains("secret@example.com"));
        if (route.Contains("invoiceId="))
        {
            var json = JsonSerializer.Deserialize<JsonElement>(text); Assert.AreEqual("Error", json.GetProperty("status").GetString());
        }
    }

    [TestMethod]
    public async Task JsonCreationAndDetailsKeepExistingShapes()
    {
        var invoice = VehicleInvoiceServiceTests.Invoice(0); invoice.GarageBusinessId = 2;
        using var response = await Send("/VehicleInvoice/AddVehicleInvoiceApi", invoice);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Success", json.GetProperty("status").GetString());
        var id = json.GetProperty("data").GetProperty("id").GetInt32();
        using var details = await Get("/VehicleInvoice/GetApi?invoiceId=" + id);
        var model = await details.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual(1, model.GetProperty("invoice").GetProperty("garageBusinessId").GetInt32());
        Assert.AreEqual("Local", model.GetProperty("invoice").GetProperty("garageBusinessCustomer").GetProperty("garageCustomerForename").GetString());
        Assert.IsTrue(model.TryGetProperty("workItems", out _));
    }

    [DataTestMethod]
    [DataRow("anonymous", 401)] [DataRow("platform", 403)] [DataRow("inactive", 403)]
    public async Task B21MembershipAuthorizationRemainsRequired(string identity, int status)
    {
        using var response = await Send("/VehicleInvoice/AddVehicleInvoiceApi", VehicleInvoiceServiceTests.Invoice(0), identity);
        Assert.AreEqual(status, (int)response.StatusCode);
    }

    [TestMethod]
    public async Task ConversionRetainsFormRequestAndJsonResponseContract()
    {
        using var response = await Send("/VehicleInvoice/CreateFromWorkQuote", new FormUrlEncodedContent(new Dictionary<string, string> { ["workQuoteId"] = "100" }));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Success", json.GetProperty("status").GetString());
        var data = json.GetProperty("data"); Assert.IsTrue(data.GetProperty("invoiceId").GetInt32() > 0); StringAssert.StartsWith(data.GetProperty("invoiceNumber").GetString()!, "INV-");
        using var duplicate = await Send("/VehicleInvoice/CreateFromWorkQuote", new FormUrlEncodedContent(new Dictionary<string, string> { ["workQuoteId"] = "100" }));
        Assert.AreEqual(HttpStatusCode.OK, duplicate.StatusCode);
        var duplicateJson = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("Error", duplicateJson.GetProperty("status").GetString());
        Assert.AreEqual("This work quote has already been converted to an invoice.", duplicateJson.GetProperty("message").GetString());
        Assert.IsFalse(duplicateJson.TryGetProperty("data", out _));
    }

    [TestMethod]
    public async Task MvcFormEditAndPaymentKeepRedirectContractsWithAntiforgery()
    {
        using var created = await Send("/VehicleInvoice/AddVehicleInvoiceApi", VehicleInvoiceServiceTests.Invoice(0));
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = json.GetProperty("data").GetProperty("id").GetInt32();
        using var edit = await SendForm("/VehicleInvoice/AddEdit", new Dictionary<string, string>
        {
            ["Invoice.Id"] = id.ToString(), ["Invoice.CustomerId"] = "101", ["Invoice.GarageBusinessCustomerId"] = "101",
            ["Invoice.VehicleId"] = "10", ["Invoice.InvoiceStatus"] = "Pending", ["Invoice.Comment"] = "Form edit", ["Invoice.Labour"] = "150"
        });
        Assert.AreEqual(HttpStatusCode.Redirect, edit.StatusCode); Assert.IsNotNull(edit.Headers.Location);
        using var paid = await SendForm("/VehicleInvoice/MarkAsPaid", new Dictionary<string, string> { ["invoiceId"] = id.ToString() });
        Assert.AreEqual(HttpStatusCode.Redirect, paid.StatusCode);
        using var db = new SignupContext(_options); var stored = db.VehicleInvoice.Single(x => x.Id == id);
        Assert.AreEqual("Form edit", stored.Comment); Assert.AreEqual("Paid", stored.InvoiceStatus);
    }

    [DataTestMethod]
    [DataRow("join", false)] [DataRow("direct", true)] [DataRow("both", false)] [DataRow("multi", true)]
    [DataRow("malformed", false)] [DataRow("none", false)] [DataRow("none", true)]
    public async Task DeleteApiPreservesEnvelopeAndProtectsAssociations(string kind, bool paid)
    {
        using var db = new SignupContext(_options);
        var invoice = VehicleInvoiceServiceTests.Invoice(0); invoice.Paid = paid; invoice.InvoiceStatus = paid ? "Paid" : "Pending";
        if (kind is "direct" or "both") invoice.WorkQuoteId = 100;
        db.VehicleInvoice.Add(invoice); db.SaveChanges();
        if (kind is "join" or "both" or "multi" or "malformed")
            db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = invoice.Id, WorkQuoteId = kind == "malformed" ? null : 100,
                GarageBusinessCustomerId = kind == "malformed" ? 2 : 1 });
        if (kind == "multi") db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = invoice.Id, WorkQuoteId = 101, GarageBusinessCustomerId = 1 });
        db.SaveChanges(); var before = JsonSerializer.Serialize(db.VehicleInvoice.AsNoTracking().Single(x => x.Id == invoice.Id));
        var links = JsonSerializer.Serialize(db.InvoiceWorkQuote.AsNoTracking().Where(x => x.InvoiceId == invoice.Id).ToList());
        using var response = await Delete(invoice.Id);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(kind == "none" ? "Success" : "Error", json.GetProperty("status").GetString());
        Assert.AreEqual(kind == "none" ? "Vehicle invoice deleted successfully" :
            "This invoice cannot be deleted because it is linked to one or more work quotes.", json.GetProperty("message").GetString());
        Assert.IsFalse(json.TryGetProperty("data", out _));
        if (kind == "none") Assert.IsFalse(db.VehicleInvoice.AsNoTracking().Any(x => x.Id == invoice.Id));
        else Assert.AreEqual(before, JsonSerializer.Serialize(db.VehicleInvoice.AsNoTracking().Single(x => x.Id == invoice.Id)));
        Assert.AreEqual(links, JsonSerializer.Serialize(db.InvoiceWorkQuote.AsNoTracking().Where(x => x.InvoiceId == invoice.Id).ToList()));
    }

    [DataTestMethod]
    [DataRow("anonymous", 401)] [DataRow("inactive", 403)] [DataRow("blocked", 403)] [DataRow("platform", 403)]
    public async Task DeleteApiRetainsAuthenticationAndMembershipBoundaries(string identity, int status)
    {
        using var response = await Delete(3, identity); Assert.AreEqual(status, (int)response.StatusCode);
    }

    [TestMethod]
    public async Task DeleteApiCannotDeleteForeignInvoice()
    {
        using var response = await Delete(3);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("Error", json.GetProperty("status").GetString());
        Assert.AreEqual("Vehicle invoice not found.", json.GetProperty("message").GetString());
        using var db = new SignupContext(_options); Assert.IsTrue(db.VehicleInvoice.Any(x => x.Id == 3));
    }

    private static async Task<HttpResponseMessage> Delete(int id, string identity = "member")
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/VehicleInvoice/DeleteApi?invoiceId=" + id);
        if (identity != "anonymous") request.Headers.Add("X-Test-Identity", identity);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return await _client.SendAsync(request);
    }

    [TestMethod]
    public async Task ManualQuoteAttachmentRetainsSuccessAndDuplicateErrorEnvelopes()
    {
        int quoteId;
        using (var db = new SignupContext(_options))
        {
            var quote = new WorkQuoteService(db).AddOrUpdateWorkQuote(WorkQuoteTenantValidationTests.Input(), 1, "seed");
            Assert.IsTrue(quote.Success); quoteId = quote.Data.WorkQuoteId;
        }
        var input = VehicleInvoiceServiceTests.Invoice(0); input.WorkQuoteId = quoteId;
        using var created = await Send("/VehicleInvoice/AddVehicleInvoiceApi", input);
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Success", json.GetProperty("status").GetString());
        StringAssert.StartsWith(json.GetProperty("data").GetProperty("invoiceNumber").GetString()!, "INV-");
        using var duplicate = await Send("/VehicleInvoice/AddVehicleInvoiceApi", input);
        Assert.AreEqual(HttpStatusCode.OK, duplicate.StatusCode);
        json = await duplicate.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Error", json.GetProperty("status").GetString());
        Assert.AreEqual("This work quote is already linked to an invoice.", json.GetProperty("message").GetString());
        Assert.IsFalse(json.TryGetProperty("data", out _));
    }

    [TestMethod]
    public async Task UpdateAttachmentKeepsMvcAndJsonContractsAndRejectsOmittedRemoval()
    {
        int quoteId;
        using (var db = new SignupContext(_options))
        {
            var quote = new WorkQuoteService(db).AddOrUpdateWorkQuote(WorkQuoteTenantValidationTests.Input(), 1, "seed");
            Assert.IsTrue(quote.Success); quoteId = quote.Data.WorkQuoteId;
        }
        using var created = await Send("/VehicleInvoice/AddVehicleInvoiceApi", VehicleInvoiceServiceTests.Invoice(0));
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = json.GetProperty("data").GetProperty("id").GetInt32();
        using var attached = await SendForm("/VehicleInvoice/AddEdit", new Dictionary<string, string>
        {
            ["Invoice.Id"] = id.ToString(), ["Invoice.CustomerId"] = "101", ["Invoice.GarageBusinessCustomerId"] = "101",
            ["Invoice.VehicleId"] = "10", ["Invoice.WorkQuoteId"] = quoteId.ToString(), ["Invoice.InvoiceStatus"] = "Pending"
        });
        Assert.AreEqual(HttpStatusCode.Redirect, attached.StatusCode);
        using var omitted = await Send("/VehicleInvoice/AddVehicleInvoiceApi", new
        { Id = id, CustomerId = 101, GarageBusinessCustomerId = 101, VehicleId = 10, InvoiceStatus = "Pending", Comment = "Rejected" });
        json = await omitted.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Error", json.GetProperty("status").GetString());
        Assert.AreEqual("Linked invoice relationships cannot be changed.", json.GetProperty("message").GetString());
        var edit = VehicleInvoiceServiceTests.Invoice(id); edit.WorkQuoteId = quoteId; edit.Comment = "Allowed";
        using var updated = await Send("/VehicleInvoice/AddVehicleInvoiceApi", edit);
        json = await updated.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Success", json.GetProperty("status").GetString());
        Assert.AreEqual("Allowed", json.GetProperty("data").GetProperty("comment").GetString());
        using var second = await Send("/VehicleInvoice/AddVehicleInvoiceApi", VehicleInvoiceServiceTests.Invoice(0));
        json = await second.Content.ReadFromJsonAsync<JsonElement>(); var secondId = json.GetProperty("data").GetProperty("id").GetInt32();
        edit = VehicleInvoiceServiceTests.Invoice(secondId); edit.WorkQuoteId = quoteId;
        using var duplicate = await Send("/VehicleInvoice/AddVehicleInvoiceApi", edit);
        json = await duplicate.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Error", json.GetProperty("status").GetString());
        Assert.AreEqual("This work quote is already linked to an invoice.", json.GetProperty("message").GetString());
    }

    private static async Task<HttpResponseMessage> SendForm(string route, Dictionary<string, string> data)
    {
        using var token = await Get("/test-token");
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = new FormUrlEncodedContent(data) };
        request.Headers.Add("X-Test-Identity", "member"); request.Headers.Add("RequestVerificationToken", await token.Content.ReadAsStringAsync());
        return await _client.SendAsync(request);
    }
    private static async Task<HttpResponseMessage> Get(string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route); request.Headers.Add("X-Test-Identity", "member"); return await _client.SendAsync(request);
    }
    private static async Task<HttpResponseMessage> Send(string route, object body, string identity = "member")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = body as HttpContent ?? JsonContent.Create(body) };
        if (identity != "anonymous") request.Headers.Add("X-Test-Identity", identity);
        return await _client.SendAsync(request);
    }
    private sealed class IdentityHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public IdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = Request.Headers["X-Test-Identity"].ToString();
            if (identity.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, identity) }, Scheme.Name)), Scheme.Name)));
        }
    }
}

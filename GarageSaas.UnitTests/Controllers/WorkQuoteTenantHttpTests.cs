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
public class WorkQuoteTenantHttpTests
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
            db.Users.AddRange(
                new Users { Id = 1, ExternalUserId = "member", FirstName = "Staff", LastName = "Test", Active = true },
                new Users { Id = 2, ExternalUserId = "platform", FirstName = "Platform", LastName = "Test", Active = true, IsPlatformAdministrator = true },
                new Users { Id = 3, ExternalUserId = "inactive", FirstName = "Inactive", LastName = "Test", Active = true },
                new Users { Id = 4, ExternalUserId = "blocked", FirstName = "Blocked", LastName = "Test", Active = true, Blocked = true },
                new Users { Id = 5, ExternalUserId = "inactive-user", FirstName = "Inactive", LastName = "User", Active = false },
                new Users { Id = 6, ExternalUserId = "ambiguous", FirstName = "Multi", LastName = "Garage", Active = true });
            foreach (var id in new[] { 1, 3, 4, 5, 6 }) db.GarageBusinessUser.Add(new GarageBusinessUser
                { UserId = id, GarageBusinessId = 1, IsActive = id != 3, Role = "Staff" });
            db.GarageBusinessUser.Add(new GarageBusinessUser { UserId = 6, GarageBusinessId = 2, IsActive = false, Role = "Staff" });
            db.WorkQuote.AddRange(new WorkQuote { Id = 300, GarageBusinessCustomerId = 1, CustomerId = 201, VehicleId = 10 },
                new WorkQuote { Id = 301, GarageBusinessCustomerId = 1, CustomerId = 101, VehicleId = 10 },
                new WorkQuote { Id = 302, GarageBusinessCustomerId = 1, CustomerId = 101, VehicleId = 10 },
                new WorkQuote { Id = 303, GarageBusinessCustomerId = 1, CustomerId = 101, VehicleId = 10 });
            db.WorkQuoteWorkItem.AddRange(new WorkQuoteWorkItem { WorkQuoteId = 300, WorkItemId = 1000, GarageBusinessCustomerId = 1 },
                new WorkQuoteWorkItem { WorkQuoteId = 301, WorkItemId = 2000, GarageBusinessCustomerId = 1 },
                new WorkQuoteWorkItem { WorkQuoteId = 302, WorkItemId = 1000, GarageBusinessCustomerId = 1 },
                new WorkQuoteWorkItem { WorkQuoteId = 303, WorkItemId = 1000, GarageBusinessCustomerId = 1 });
            var foreignInvoice = VehicleInvoiceServiceTests.Invoice(1); foreignInvoice.GarageBusinessId = 2;
            foreignInvoice.CustomerId = foreignInvoice.GarageBusinessCustomerId = 201; foreignInvoice.VehicleId = 20; foreignInvoice.WorkQuoteId = 200;
            var directInvoice = VehicleInvoiceServiceTests.Invoice(2); directInvoice.WorkQuoteId = 303;
            db.VehicleInvoice.AddRange(foreignInvoice, directInvoice);
            db.InvoiceWorkQuote.Add(new InvoiceWorkQuote { InvoiceId = 1, WorkQuoteId = 302, GarageBusinessCustomerId = 1 });
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
                services.AddScoped<IWorkQuoteService, WorkQuoteService>();
                services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, IdentityHandler>("Test", _ => { });
                services.AddAuthorization();
                services.AddControllersWithViews().AddApplicationPart(typeof(WorkQuoteController).Assembly);
            });
            web.Configure(app =>
            {
                app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseMiddleware<GarageAccessMiddleware>();
                app.UseEndpoints(routes => routes.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"));
            });
        }).StartAsync();
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        { BaseAddress = new Uri(_host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }
    [ClassCleanup]
    public static async Task Cleanup()
    {
        _client?.Dispose(); if (_host != null) { await _host.StopAsync(); _host.Dispose(); }
    }

    [DataTestMethod]
    [DataRow("anonymous", 401)] [DataRow("platform", 403)] [DataRow("inactive", 403)]
    [DataRow("blocked", 403)] [DataRow("inactive-user", 403)] [DataRow("ambiguous", 403)]
    public async Task ExistingAuthenticationAndMembershipBoundariesApplyToQuoteActions(string identity, int status)
    {
        using var read = await Send(HttpMethod.Get, "/WorkQuote/Get?workQuoteId=100", null, identity);
        Assert.AreEqual(status, (int)read.StatusCode);
        using var write = await Send(HttpMethod.Post, "/WorkQuote/AddWorkQuote", WorkQuoteTenantValidationTests.Input(), identity);
        Assert.AreEqual(status, (int)write.StatusCode);
        if (status == 403)
        {
            Assert.AreEqual("application/json", write.Content.Headers.ContentType?.MediaType);
            var body = await write.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Error", body.GetProperty("status").GetString());
            Assert.IsNull(write.Headers.Location);
        }
    }

    [DataTestMethod]
    [DataRow(200)] [DataRow(300)] [DataRow(301)] [DataRow(302)] [DataRow(999)]
    public async Task SingleQuoteReadsReturnSafeExistingErrorEnvelope(int id)
    {
        using var response = await Send(HttpMethod.Get, "/WorkQuote/Get?workQuoteId=" + id);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("Error", json.GetProperty("status").GetString()); Assert.AreEqual("WorkQuote not found.", json.GetProperty("message").GetString());
        Assert.IsFalse(json.TryGetProperty("data", out _));
    }

    [TestMethod]
    public async Task ListsKeepVehicleEnvelopeAndWorkItemArrayWithoutForeignData()
    {
        using var vehicle = await Send(HttpMethod.Get, "/WorkQuote/GetByVehicle?vehicleId=10");
        var json = await vehicle.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Success", json.GetProperty("status").GetString());
        Assert.IsFalse((await vehicle.Content.ReadAsStringAsync()).Contains("SECRET"));
        Assert.IsFalse(json.GetProperty("data").EnumerateArray().Any(x => new[] { 300, 301, 302 }.Contains(x.GetProperty("workQuoteId").GetInt32())));
        using var item = await Send(HttpMethod.Get, "/WorkQuote/GetByWorkItem?workItemId=1000");
        var array = await item.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual(JsonValueKind.Array, array.ValueKind);
        Assert.IsTrue(array.EnumerateArray().Any(x => x.GetProperty("workQuoteId").GetInt32() == 100));
        Assert.IsFalse(array.EnumerateArray().Any(x => new[] { 300, 301, 302 }.Contains(x.GetProperty("workQuoteId").GetInt32())));
    }

    [TestMethod]
    public async Task ValidDirectOnlyInvoiceStateUsesExistingDtoFields()
    {
        using var response = await Send(HttpMethod.Get, "/WorkQuote/Get?workQuoteId=303");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Success", json.GetProperty("status").GetString());
        Assert.IsTrue(json.GetProperty("data").GetProperty("hasInvoice").GetBoolean());
        Assert.AreEqual(2, json.GetProperty("data").GetProperty("invoiceId").GetInt32());
    }

    [DataTestMethod]
    [DataRow("/WorkQuote/GetByVehicle?vehicleId=20", 200)]
    [DataRow("/WorkQuote/GetByWorkItem?workItemId=2000", 200)]
    [DataRow("/WorkQuote/GetVehiclesForCustomer?garageCustomerId=201", 404)]
    public async Task ForeignLookupRootsKeepSafeErrorContracts(string route, int status)
    {
        using var response = await Send(HttpMethod.Get, route); Assert.AreEqual(status, (int)response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Error", json.GetProperty("status").GetString());
        Assert.IsFalse(json.TryGetProperty("data", out _));
    }

    [DataTestMethod]
    [DataRow("customer", 201)] [DataRow("customer", 999)] [DataRow("vehicle", 20)] [DataRow("vehicle", 999)]
    [DataRow("item", 2000)] [DataRow("item", 999)]
    public async Task JsonWritesRejectForeignOrMissingIdsWithoutChanges(string field, int id)
    {
        using var db = new SignupContext(_options); var before = State(db);
        var model = WorkQuoteTenantValidationTests.Input(100);
        if (field == "customer") model.CustomerId = id;
        if (field == "vehicle") model.VehicleId = id;
        if (field == "item") model.WorkItemIds = new() { id };
        using var response = await Send(HttpMethod.Post, "/WorkQuote/AddWorkQuote", model);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Error", json.GetProperty("status").GetString());
        Assert.AreEqual(before, State(db));
    }

    [TestMethod]
    public async Task StaffCanCreateEditAndDeleteAndLegacySingleItemContractStillWorks()
    {
        var input = WorkQuoteTenantValidationTests.Input(); input.GarageBusinessCustomerId = 2;
        input.WorkItemIds.Clear(); input.WorkItemId = 1000;
        using var created = await Send(HttpMethod.Post, "/WorkQuote/AddWorkQuote3", input);
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Success", json.GetProperty("status").GetString());
        Assert.IsTrue(json.TryGetProperty("message", out _)); var data = json.GetProperty("data");
        Assert.AreEqual(1, data.GetProperty("garageBusinessCustomerId").GetInt32());
        var id = data.GetProperty("workQuoteId").GetInt32(); Assert.AreEqual(1000, data.GetProperty("workItemId").GetInt32());
        var edit = WorkQuoteTenantValidationTests.Input(id); edit.WorkItemIds.Add(1001); edit.Comment = "Edited";
        using var updated = await Send(HttpMethod.Post, "/WorkQuote/AddWorkQuote", edit);
        Assert.AreEqual("Success", (await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        using var read = await Send(HttpMethod.Get, "/WorkQuote/Get?workQuoteId=" + id);
        Assert.AreEqual(2, (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("workItems").GetArrayLength());
        using var deleted = await Send(HttpMethod.Delete, "/WorkQuote/Delete?workQuoteId=" + id);
        var result = await deleted.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Success", result.GetProperty("status").GetString());
        Assert.AreEqual("WorkQuote deleted successfully", result.GetProperty("message").GetString());
    }

    [DataTestMethod]
    [DataRow("/WorkQuote/AddWorkQuote")] [DataRow("/WorkQuote/AddWorkQuote3")]
    public async Task LinkedLegacyQuoteRejectsRetargetingAndDeletionWithExistingErrorEnvelope(string route)
    {
        using var db = new SignupContext(_options); var before = State(db);
        var invoices = JsonSerializer.Serialize(db.VehicleInvoice.AsNoTracking().OrderBy(x => x.Id).ToList());
        var input = WorkQuoteTenantValidationTests.Input(303); input.WorkItemIds = new() { 1001 };
        using var changed = await Send(HttpMethod.Post, route, input);
        Assert.AreEqual(HttpStatusCode.OK, changed.StatusCode);
        var json = await changed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("Error", json.GetProperty("status").GetString());
        Assert.AreEqual("Invoice-linked work quote relationships cannot be changed.", json.GetProperty("message").GetString());
        Assert.IsFalse(json.TryGetProperty("data", out _));
        using var deleted = await Send(HttpMethod.Delete, "/WorkQuote/Delete?workQuoteId=303");
        Assert.AreEqual(HttpStatusCode.OK, deleted.StatusCode);
        json = await deleted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("Error", json.GetProperty("status").GetString());
        Assert.AreEqual("Invoice-linked work quotes cannot be deleted.", json.GetProperty("message").GetString());
        Assert.AreEqual(before, State(db));
        Assert.AreEqual(invoices, JsonSerializer.Serialize(db.VehicleInvoice.AsNoTracking().OrderBy(x => x.Id).ToList()));
    }

    [TestMethod]
    public async Task LinkedQuoteAllowsLegacySameRelationshipEditWithExistingSuccessEnvelope()
    {
        var input = WorkQuoteTenantValidationTests.Input(303); input.WorkItemIds.Clear(); input.WorkItemId = 1000;
        input.Comment = "Allowed linked note"; input.Labour = 444;
        using var updated = await Send(HttpMethod.Post, "/WorkQuote/AddWorkQuote3", input);
        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode);
        var json = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("Success", json.GetProperty("status").GetString()); Assert.IsTrue(json.TryGetProperty("message", out _));
        Assert.IsTrue(json.GetProperty("data").GetProperty("hasInvoice").GetBoolean());
        Assert.AreEqual(2, json.GetProperty("data").GetProperty("invoiceId").GetInt32());
    }

    private static string State(SignupContext db) => JsonSerializer.Serialize(new
    {
        quotes = db.WorkQuote.AsNoTracking().OrderBy(x => x.Id).ToList(),
        links = db.WorkQuoteWorkItem.AsNoTracking().OrderBy(x => x.Id).ToList()
    });
    private static async Task<HttpResponseMessage> Send(HttpMethod method, string route, object? body = null, string identity = "member")
    {
        using var request = new HttpRequestMessage(method, route);
        if (body != null) request.Content = JsonContent.Create(body);
        if (identity != "anonymous") request.Headers.Add("X-Test-Identity", identity);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return await _client.SendAsync(request);
    }
    private sealed class IdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = Request.Headers["X-Test-Identity"].ToString();
            if (string.IsNullOrWhiteSpace(identity)) return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, identity) }, "Test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
        }
    }
}

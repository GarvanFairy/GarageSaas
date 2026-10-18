using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using GarageSaas.Controllers;
using GarageSaas.Middleware;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class WorkItemTenantHttpTests
{
    private static IHost _host = null!;
    private static HttpClient _client = null!;
    private static DbContextOptions<SignupContext> _options = null!;

    [ClassInitialize]
    public static async Task Initialize(TestContext _)
    {
        _options = new DbContextOptionsBuilder<SignupContext>().UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options;
        using (var db = new SignupContext(_options))
        {
            GarageSaas.Tests.Services.WorkItemServiceTests.SeedRelationships(db);
            db.Users.AddRange(new Users { Id = 1, ExternalUserId = "member", FirstName = "Member", LastName = "Test", Active = true },
                new Users { Id = 2, ExternalUserId = "platform", FirstName = "Platform", LastName = "Test", Active = true, IsPlatformAdministrator = true });
            db.GarageBusinessUser.Add(new GarageBusinessUser { UserId = 1, GarageBusinessId = 5, IsActive = true, Role = "Staff" });
            db.WorkItem.AddRange(new WorkItem { Id = 1, GarageBusinessCustomerId = 5, VehicleId = 100, CustomerId = 10, RepairInstructions = "Local" },
                new WorkItem { Id = 2, GarageBusinessCustomerId = 99, VehicleId = 900, CustomerId = 90, RepairInstructions = "Private" },
                new WorkItem { Id = 3, GarageBusinessCustomerId = 5, VehicleId = 900, CustomerId = 10, RepairInstructions = "Inconsistent" });
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
                services.AddScoped<IWorkItemService, WorkItemService>();
                services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, IdentityHandler>("Test", _ => { });
                services.AddAuthorization();
                services.AddControllersWithViews().AddApplicationPart(typeof(WorkItemController).Assembly);
            });
            web.Configure(app =>
            {
                app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseMiddleware<GarageAccessMiddleware>();
                app.UseEndpoints(endpoints => endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"));
            });
        }).StartAsync();
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        { BaseAddress = new Uri(_host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }

    [ClassCleanup]
    public static async Task Cleanup() { _client?.Dispose(); if (_host != null) { await _host.StopAsync(); _host.Dispose(); } }

    [DataTestMethod]
    [DataRow("/WorkItem/AddWorkItem", 900, 10)] [DataRow("/WorkItem/AddWorkItem", 100, 90)]
    [DataRow("/WorkItem/SaveForQuote", 900, 10)] [DataRow("/WorkItem/SaveForQuote", 100, 90)]
    [DataRow("/WorkItem/SaveForQuote", 100, 0)]
    public async Task JsonCreatesRejectForgedRelationshipsWithoutWrites(string route, int vehicle, int customer)
    {
        using var db = new SignupContext(_options); var count = db.WorkItem.Count();
        using var response = await Send(route, new { id = 0, vehicleId = vehicle, customerId = customer, garageBusinessCustomerId = 99, repairInstructions = "forged" });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Error", json.GetProperty("status").GetString());
        Assert.AreEqual(count, db.WorkItem.Count());
    }

    [DataTestMethod]
    [DataRow("/WorkItem/AddWorkItem")]
    [DataRow("/WorkItem/SaveForQuote")]
    public async Task RealJsonCallersRetainSuccessShapeAndTrustedTenant(string route)
    {
        using var response = await Send(route, new { vehicleId = 100, customerId = 10, garageBusinessCustomerId = 99, repairInstructions = "HTTP work item" });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Success", json.GetProperty("status").GetString());
        var id = json.GetProperty("data").GetProperty("id").GetInt32();
        using var db = new SignupContext(_options); var item = db.WorkItem.Single(x => x.Id == id);
        Assert.AreEqual(5, item.GarageBusinessCustomerId); Assert.AreEqual(100, item.VehicleId); Assert.AreEqual(10, item.CustomerId);
    }

    [DataTestMethod]
    [DataRow("/WorkItem/Get?workItemId=2")]
    [DataRow("/WorkItem/Get?workItemId=3")]
    public async Task UnsafePrimaryOrRelatedRecordsAreNotReturned(string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route); request.Headers.Add("X-Test-Identity", "member");
        using var response = await _client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("Private"));
    }

    [DataTestMethod]
    [DataRow("anonymous", 401)] [DataRow("platform", 403)] [DataRow("unknown", 403)]
    public async Task B21MembershipAuthorizationStillApplies(string identity, int expected)
    {
        using var response = await Send("/WorkItem/AddWorkItem", new { vehicleId = 100, customerId = 10, repairInstructions = "work" }, identity);
        Assert.AreEqual(expected, (int)response.StatusCode);
    }

    [TestMethod]
    public async Task QuoteModalDescriptionEditIgnoresReplacementIds()
    {
        using var response = await Send("/WorkItem/SaveForQuote", new { id = 1, vehicleId = 900, customerId = 90, repairInstructions = "Updated description" });
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.AreEqual("Success", json.GetProperty("status").GetString());
        Assert.AreEqual("Updated description", json.GetProperty("data").GetProperty("repairInstructions").GetString());
        using var db = new SignupContext(_options); var item = db.WorkItem.Single(x => x.Id == 1);
        Assert.AreEqual(100, item.VehicleId); Assert.AreEqual(10, item.CustomerId);
    }

    private static async Task<HttpResponseMessage> Send(string route, object body, string identity = "member")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
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

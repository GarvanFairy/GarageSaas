using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using GarageSaas.Controllers;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Antiforgery;
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
using GarageSaas.Authorization;
using GarageSaas.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GarageSaas.UnitTests.Controllers;

// A real loopback HTTP host exercises MVC routing, authentication and filters without
// B2C/network identity calls or SQL Server. Feature DbSets and all writes throw.
[TestClass]
public class RetiredTenantEndpointTests
{
    private static IHost _host = null!;
    private static HttpClient _client = null!;
    private static readonly (string Route, string Method)[] Routes =
    {
        ("/VehiclePart/Get", "GET"), ("/VehiclePart/GetAll", "GET"),
        ("/VehiclePart/AddVehiclePart", "POST"), ("/VehiclePart/Delete", "DELETE"),
        ("/ServiceHistory/Create", "POST"), ("/ServiceHistory/Get", "GET"),
        ("/ServiceHistory/GetServiceHistoryByVehicleId", "GET")
    };

    [ClassInitialize]
    public static async Task Initialize(TestContext _)
    {
        var options = new DbContextOptionsBuilder<SignupContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
            // Invalid-token boundary tests enter the existing invitation transaction.
            // They perform no writes; SQL transaction semantics are not tested here.
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        using (var seed = new SignupContext(options))
        {
            seed.GarageBusiness.Add(new GarageBusiness { Id = 1, GarageBusinessName = "Test", Active = true });
            seed.GarageBusiness.Add(new GarageBusiness { Id = 2, GarageBusinessName = "Inactive", Active = false });
            seed.GarageBusiness.Add(new GarageBusiness { Id = 3, GarageBusinessName = "Blocked", Active = true, Blocked = true });
            var states = new[] { "eligible", "no-membership", "inactive-membership", "blocked-user", "inactive-user", "platform-admin", "inactive-garage", "blocked-garage", "multiple", "one-eligible", "missing-garage", "owner" };
            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i];
                seed.Users.Add(new Users
                {
                    Id = i + 1, ExternalUserId = state, FirstName = "Test", LastName = "User",
                    Active = state != "inactive-user", Blocked = state == "blocked-user",
                    IsPlatformAdministrator = state == "platform-admin"
                });
                if (state != "no-membership" && state != "platform-admin")
                    seed.GarageBusinessUser.Add(new GarageBusinessUser
                    {
                        UserId = i + 1,
                        GarageBusinessId = state == "inactive-garage" ? 2 : state == "blocked-garage" ? 3 : state == "missing-garage" ? 999 : 1,
                        IsActive = state != "inactive-membership", Role = state == "owner" ? "Owner" : "Staff", IsOwner = state == "owner"
                    });
                if (state == "multiple" || state == "one-eligible")
                    seed.GarageBusinessUser.Add(new GarageBusinessUser
                    {
                        UserId = i + 1, GarageBusinessId = 2, IsActive = state == "multiple", Role = "Staff"
                    });
            }
            seed.SaveChanges();
        }
        _host = await new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseKestrel(server => server.Listen(IPAddress.Loopback, 0));
            web.ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddHttpContextAccessor();
                services.AddScoped<GarageSelectionService>();
                services.AddScoped<SignupContext>(_ => new GuardedContext(options));
                services.AddScoped<ICurrentGarageUserService, CurrentGarageUserService>();
                services.AddScoped<IGarageAuthorizationService, GarageAuthorizationService>();
                services.AddScoped<IGarageBusinessService, GarageBusinessService>();
                services.AddSingleton(Mock.Of<IEmailService>());
                services.AddSingleton(Mock.Of<IRegistrationRequestLock>());
                services.AddSingleton(Mock.Of<IInvitationIdentityVerificationService>());
                services.AddScoped<OnboardingInvitationService>(_ => new OnboardingInvitationService(
                    new ConfigurationBuilder().Build(), Mock.Of<IEmailService>(), NullLogger<OnboardingInvitationService>.Instance));
                // IVehiclePartService is deliberately absent: retired actions cannot resolve it.
                services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
                services.AddAuthorization(options => options.AddPolicy("PlatformAdministrator", policy =>
                    policy.RequireAuthenticatedUser().AddRequirements(new PlatformAdministratorRequirement())));
                services.AddScoped<IAuthorizationHandler, PlatformAdministratorHandler>();
                services.AddControllersWithViews().AddApplicationPart(typeof(VehiclePartController).Assembly);
            });
            web.Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseMiddleware<GarageAccessMiddleware>();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
                    // Test-only token issuance lets mutation tests submit a genuine cookie/token pair.
                    endpoints.MapGet("/test-token", async context =>
                    {
                        var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
                        await context.Response.WriteAsync(tokens.RequestToken!);
                    });
                });
            });
        }).StartAsync();
        var address = _host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        _client?.Dispose();
        if (_host != null) { await _host.StopAsync(); _host.Dispose(); }
    }

    public static IEnumerable<object[]> AccessCases()
    {
        foreach (var (route, method) in Routes)
        foreach (var state in new[] { "anonymous", "eligible", "no-membership", "inactive-membership", "blocked-user", "inactive-user", "platform-admin", "unknown-user", "missing-identifier", "inactive-garage", "blocked-garage", "multiple", "one-eligible", "missing-garage" })
            yield return new object[] { route, method, state };
    }

    [DataTestMethod]
    [DynamicData(nameof(AccessCases), DynamicDataSourceType.Method)]
    public async Task Http_EnforcesAuthenticationAndMembershipBeforeGone(string route, string method, string state)
    {
        using var request = Request(route, method, state);
        using var response = await _client.SendAsync(request);
        var expected = state == "anonymous" ? 401 : state == "eligible" ? 410 : 403;
        Assert.AreEqual(expected, (int)response.StatusCode, route + " " + state);
        if (expected == 410)
        {
            Assert.AreEqual("{\"message\":\"This functionality is temporarily unavailable.\"}", await response.Content.ReadAsStringAsync());
            Assert.IsTrue(response.Headers.CacheControl!.NoStore);
        }
    }

    public static IEnumerable<object[]> MutationCases()
    {
        foreach (var (route, method) in Routes.Where(x => x.Method != "GET"))
        foreach (var validToken in new[] { false, true })
            yield return new object[] { route, method, validToken };
    }

    [DataTestMethod]
    [DynamicData(nameof(MutationCases), DynamicDataSourceType.Method)]
    public async Task Http_MutationsIgnoreForgedPayloadEvenWithValidAntiforgery(string route, string method, bool validToken)
    {
        using var request = Request(route, method, "eligible");
        if (validToken)
        {
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, "/test-token");
            tokenRequest.Headers.Add("X-Test-Identity", "eligible");
            using var tokenResponse = await _client.SendAsync(tokenRequest);
            request.Headers.Add("RequestVerificationToken", await tokenResponse.Content.ReadAsStringAsync());
        }
        using var response = await _client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Gone, response.StatusCode);
        Assert.AreEqual("{\"message\":\"This functionality is temporarily unavailable.\"}", await response.Content.ReadAsStringAsync());
    }

    public static IEnumerable<object[]> VerbCases()
    {
        foreach (var route in new[] { "/VehiclePart/Get", "/VehiclePart/GetAll" })
            yield return new object[] { route, "POST", 405 };
        foreach (var route in new[] { "/VehiclePart/AddVehiclePart", "/VehiclePart/Delete", "/ServiceHistory/Create" })
            yield return new object[] { route, "GET", 405 };
        foreach (var route in new[] { "/ServiceHistory/Get", "/ServiceHistory/GetServiceHistoryByVehicleId" })
        foreach (var method in new[] { "POST", "PUT", "DELETE" })
            yield return new object[] { route, method, 410 };
    }

    [DataTestMethod]
    [DynamicData(nameof(VerbCases), DynamicDataSourceType.Method)]
    public async Task Http_PreservesVerbMappings(string route, string method, int status)
    {
        using var request = Request(route + "/123", method, "eligible");
        using var response = await _client.SendAsync(request);
        Assert.AreEqual(status, (int)response.StatusCode);
    }

    [DataTestMethod]
    [DataRow("no-membership")]
    [DataRow("multiple")]
    [DataRow("one-eligible")]
    [DataRow("inactive-garage")]
    [DataRow("missing-identifier")]
    public async Task HtmlNavigationDenialReachesRecoveryPageWithoutLoop(string state)
    {
        using var request = Request("/Home/Index", "GET", state);
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await _client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        var selectionRequired = state == "multiple" || state == "one-eligible";
        Assert.AreEqual(selectionRequired ? "/GarageSelection" : "/Home/NoGarageAccess", location);        using var next = new HttpRequestMessage(HttpMethod.Get, location);
        next.Headers.Add("X-Test-Identity", state);
        next.Headers.Accept.ParseAdd("text/html");
        using var page = await _client.SendAsync(next);
        Assert.AreEqual(HttpStatusCode.OK, page.StatusCode);
        Assert.IsNull(page.Headers.Location);
        if (state == "multiple" || state == "one-eligible")
            StringAssert.Contains(await page.Content.ReadAsStringAsync(), "Choose a garage");
    }

    [DataTestMethod]
    [DataRow("GET", "*/*")]
    [DataRow("GET", "application/json")]
    [DataRow("POST", "text/html")]
    public async Task FetchAndUnsafeDenialsReturnJsonWithoutRedirect(string method, string accept)
    {
        using var request = Request("/Home/Index", method, "multiple");
        request.Headers.Accept.ParseAdd(accept);
        using var response = await _client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.IsNull(response.Headers.Location);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType!.MediaType);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "GarageSelectionRequired");
    }

    [DataTestMethod]
    [DataRow("/PlatformAdmin/Garages", "platform-admin")]
    [DataRow("/PlatformAdmin/RegistrationRequests", "platform-admin")]
    [DataRow("/Home/Index", "eligible")]
    [DataRow("/GarageBusiness/GarageBusinessDetail", "eligible")]
    [DataRow("/GarageBusiness/EditGarageBusiness", "owner")]
    [DataRow("/GarageTeam/Index", "owner")]
    [DataRow("/GarageRegistration/Register", "anonymous")]
    [DataRow("/GarageOwnerOnboarding/Accept?token=invalid", "anonymous")]
    [DataRow("/GarageInvitation/Accept?token=invalid", "anonymous")]
    public async Task ExistingPagesRenderWithoutTenantPolicyChanges(string route, string identity)
    {
        using var request = Request(route, "GET", identity);
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await _client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, route);
        if (route.StartsWith("/PlatformAdmin"))
            StringAssert.Contains(await response.Content.ReadAsStringAsync(), "Garage Management");
    }

    [TestMethod]
    public async Task PlatformOnlyAdministratorIsDeniedTenantDataAndStaffCannotEditProfile()
    {
        using var tenant = Request("/GarageBusiness/GarageBusinessDetail", "GET", "platform-admin");
        using var denied = await _client.SendAsync(tenant);
        Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode);
        using var edit = Request("/GarageBusiness/EditGarageBusiness", "GET", "eligible");
        using var forbidden = await _client.SendAsync(edit);
        Assert.AreEqual(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
    [DataTestMethod]
    [DataRow("/GarageRegistration/Register", "anonymous")]
    [DataRow("/GarageOwnerOnboarding/Complete", "platform-admin")]
    [DataRow("/GarageInvitation/Complete", "platform-admin")]
    public async Task RegistrationAndInvitationPostsDoNotRequireExistingMembership(string route, string identity)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, "/test-token");
        if (identity != "anonymous") tokenRequest.Headers.Add("X-Test-Identity", identity);
        using var tokenResponse = await _client.SendAsync(tokenRequest);
        using var request = new HttpRequestMessage(HttpMethod.Post, route);
        if (identity != "anonymous") request.Headers.Add("X-Test-Identity", identity);
        request.Headers.Add("RequestVerificationToken", await tokenResponse.Content.ReadAsStringAsync());
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = "invalid" });
        using var response = await _client.SendAsync(request);
        // Invalid registration input/token is handled by the original view flow,
        // without invoking guarded writes or demanding an existing tenant context.
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
    private static HttpRequestMessage Request(string route, string method, string state)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), route + "?vehiclePartId=999&serviceHistoryId=-1&vehicleId=invalid&GarageBusinessId=999&CreatedBy=forged");
        if (state != "anonymous") request.Headers.Add("X-Test-Identity", state);
        if (method != "GET") request.Content = new StringContent("{\"Id\":999,\"VehicleId\":999,\"GarageBusinessId\":999,\"CreatedBy\":\"forged\",\"CreatedDate\":\"invalid\"}", Encoding.UTF8, "application/json");
        return request;
    }

    private sealed class GuardedContext : SignupContext
    {
        public GuardedContext(DbContextOptions<SignupContext> options) : base(options) { }
        public override DbSet<VehiclePart> VehiclePart { get => throw new AssertFailedException("Parts must not be read."); set { } }
        public override DbSet<ServiceHistory> ServiceHistory { get => throw new AssertFailedException("History must not be read."); set { } }
        public override int SaveChanges() => throw new AssertFailedException("No writes permitted.");
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new AssertFailedException("No writes permitted.");
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var state = Request.Headers["X-Test-Identity"].ToString();
            if (state.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = state == "missing-identifier" ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.NameIdentifier, state) };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}

using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GarageSaas.Controllers;
using GarageSaas.Middleware;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class StaffInvitationHttpTests
{
    [DataTestMethod]
    [DataRow("active", "get")] [DataRow("inactive", "get")] [DataRow("blocked", "get")] [DataRow("missing", "get")]
    [DataRow("active", "signin")] [DataRow("inactive", "signin")] [DataRow("blocked", "signin")] [DataRow("missing", "signin")]
    [DataRow("active", "complete")] [DataRow("inactive", "complete")] [DataRow("blocked", "complete")] [DataRow("missing", "complete")]
    public async Task AvailabilityAndNewUserAcceptanceThroughRealMvc(string garage, string action)
    {
        await using var host = await StaffHost.Start(garage: garage, userState: "new");
        using var beforeDb = new SignupContext(host.Options); var before = StaffInvitationData.Snapshot(beforeDb);
        using var response = action == "get" ? await host.Send("GET", "/GarageInvitation/Accept?token=" + StaffInvitationData.Token, null)
            : await host.Send("POST", "/GarageInvitation/" + (action == "signin" ? "SignIn" : "Complete"), action == "signin" ? null : "invitee",
                new() { ["token"] = StaffInvitationData.Token, ["GarageBusinessId"] = "999", ["Role"] = "Owner", ["UserId"] = "1", ["IsOwner"] = "true" });
        var eligible = garage == "active";
        Assert.AreEqual(eligible && action != "get" ? HttpStatusCode.Redirect : HttpStatusCode.OK, response.StatusCode);
        using var check = new SignupContext(host.Options);
        if (!eligible)
        { StringAssert.Contains(await response.Content.ReadAsStringAsync(), "This invitation is no longer valid"); Assert.AreEqual(before, StaffInvitationData.Snapshot(check)); }
        else if (action == "complete")
        {
            Assert.AreEqual("/", response.Headers.Location!.OriginalString);
            var user = check.Users.Single(x => x.ExternalUserId == "invitee"); var membership = check.GarageBusinessUser.Single(x => x.UserId == user.Id);
            Assert.AreEqual(1, membership.GarageBusinessId); Assert.AreEqual("Staff", membership.Role); Assert.IsFalse(membership.IsOwner); Assert.IsTrue(membership.IsActive);
            Assert.IsTrue(check.GarageUserInvitation.Single().Accepted);
        }
        else if (action == "signin") Assert.AreEqual("/test-b2c", response.Headers.Location!.OriginalString);
        else StringAssert.Contains(await response.Content.ReadAsStringAsync(), "Invited garage");
    }

    [DataTestMethod]
    [DataRow("inactive")] [DataRow("blocked")] [DataRow("missing")]
    public async Task GarageBecomingUnavailableAfterIssueCannotConsumeOrCreateUser(string state)
    {
        await using var host = await StaffHost.Start();
        using var issued = await host.Send("POST", "/GarageTeam/Invite", "admin", StaffHost.InviteFields(), forgedHost: true);
        Assert.AreEqual(HttpStatusCode.Redirect, issued.StatusCode); Assert.AreEqual(1, host.Urls.Count);
        var uri = new Uri(host.Urls.Single()); Assert.AreEqual("trusted.example", uri.Authority); Assert.AreEqual("https", uri.Scheme);
        var token = QueryHelpers.ParseQuery(uri.Query)["token"].ToString();
        using (var change = new SignupContext(host.Options))
        { var g = change.GarageBusiness.Single(); if (state == "missing") change.Remove(g); else { g.Active = state != "inactive"; g.Blocked = state == "blocked"; } change.SaveChanges(); }
        using var beforeDb = new SignupContext(host.Options); var before = StaffInvitationData.Snapshot(beforeDb);
        using var rejected = await host.Send("POST", "/GarageInvitation/Complete", "new", new() { ["token"] = token });
        Assert.AreEqual(HttpStatusCode.OK, rejected.StatusCode); StringAssert.Contains(await rejected.Content.ReadAsStringAsync(), "This invitation is no longer valid");
        using var check = new SignupContext(host.Options); Assert.AreEqual(before, StaffInvitationData.Snapshot(check)); Assert.IsFalse(check.Users.Any(x => x.ExternalUserId == "new"));
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public async Task LowercaseAdministratorAndTrustedAuthorityWorkForCreateAndResend(bool resend)
    {
        await using var host = await StaffHost.Start(role: "administrator");
        using var result = await host.Send("POST", resend ? "/GarageTeam/ResendInvitation" : "/GarageTeam/Invite", "admin",
            resend ? new() { ["invitationId"] = "1", ["GarageBusinessId"] = "999" } : StaffHost.InviteFields(), forgedHost: true);
        Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode); Assert.AreEqual(1, host.Urls.Count);
        var url = new Uri(host.Urls.Single()); Assert.AreEqual("https://trusted.example", url.GetLeftPart(UriPartial.Authority)); Assert.AreEqual("/GarageInvitation/Accept", url.AbsolutePath);
        using var newLink = await host.Send("GET", url.PathAndQuery, null);
        Assert.AreEqual(HttpStatusCode.OK, newLink.StatusCode); StringAssert.Contains(await newLink.Content.ReadAsStringAsync(), "Invited garage");
        using var check = new SignupContext(host.Options);
        if (resend)
        {
            Assert.AreNotEqual(GarageInvitationTokenService.HashToken(StaffInvitationData.Token), check.GarageUserInvitation.Single().InvitationTokenHash);
            using var old = await host.Send("GET", "/GarageInvitation/Accept?token=" + StaffInvitationData.Token, null);
            StringAssert.Contains(await old.Content.ReadAsStringAsync(), "This invitation is no longer valid");
        }
        else Assert.AreEqual(1, check.GarageUserInvitation.Single(x => x.EmailAddress == "new@test.invalid").GarageBusinessId);
    }

    [DataTestMethod]
    [DataRow(null, false)] [DataRow("http://trusted.example", false)] [DataRow("https://trusted.example/path", false)]
    [DataRow(null, true)] [DataRow("http://trusted.example", true)] [DataRow("https://trusted.example/path", true)]
    public async Task InvalidOriginFailsWithoutWritesOrEmail(string? origin, bool resend)
    {
        await using var host = await StaffHost.Start(origin: origin);
        using var beforeDb = new SignupContext(host.Options); var before = StaffInvitationData.Snapshot(beforeDb);
        using var result = await host.Send("POST", resend ? "/GarageTeam/ResendInvitation" : "/GarageTeam/Invite", "admin",
            resend ? new() { ["invitationId"] = "1" } : StaffHost.InviteFields(), forgedHost: true);
        Assert.AreEqual(resend ? HttpStatusCode.Redirect : HttpStatusCode.OK, result.StatusCode);
        if (!resend) StringAssert.Contains(await result.Content.ReadAsStringAsync(), "trusted HTTPS invitation origin");
        using var check = new SignupContext(host.Options); Assert.AreEqual(before, StaffInvitationData.Snapshot(check)); Assert.AreEqual(0, host.Urls.Count);
    }

    [DataTestMethod]
    [DataRow("expired")] [DataRow("revoked")] [DataRow("accepted")] [DataRow("rotated")]
    public async Task InvalidInvitationStatesCannotBeConsumedThroughHttp(string state)
    {
        await using var host = await StaffHost.Start(invitation: state);
        using var beforeDb = new SignupContext(host.Options); var before = StaffInvitationData.Snapshot(beforeDb);
        using var response = await host.Send("POST", "/GarageInvitation/Complete", "invitee", new() { ["token"] = StaffInvitationData.Token });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode); StringAssert.Contains(await response.Content.ReadAsStringAsync(), "This invitation is no longer valid");
        using var check = new SignupContext(host.Options); Assert.AreEqual(before, StaffInvitationData.Snapshot(check));
    }

    [DataTestMethod]
    [DataRow("blocked")] [DataRow("inactive")] [DataRow("inactive-membership")] [DataRow("wrong-email")]
    public async Task ExistingUserRestrictionsAreHttpForbiddenWithoutWrites(string state)
    {
        await using var host = await StaffHost.Start(userState: state);
        using var beforeDb = new SignupContext(host.Options); var before = StaffInvitationData.Snapshot(beforeDb);
        using var result = await host.Send("POST", "/GarageInvitation/Complete", state == "wrong-email" ? "wrong" : "invitee", new() { ["token"] = StaffInvitationData.Token });
        Assert.AreEqual(HttpStatusCode.Forbidden, result.StatusCode);
        using var check = new SignupContext(host.Options); Assert.AreEqual(before, StaffInvitationData.Snapshot(check));
    }

    [TestMethod]
    public async Task MultipleMembershipAcceptanceStillHasSelectionRequiredOnNextTenantRequest()
    {
        await using var host = await StaffHost.Start(userState: "multiple");
        using var accepted = await host.Send("POST", "/GarageInvitation/Complete", "invitee", new() { ["token"] = StaffInvitationData.Token });
        Assert.AreEqual(HttpStatusCode.Redirect, accepted.StatusCode);
        using var tenant = await host.Send("GET", "/test/current", "invitee");
        Assert.AreEqual(HttpStatusCode.Forbidden, tenant.StatusCode); StringAssert.Contains(await tenant.Content.ReadAsStringAsync(), "GarageSelectionRequired");
    }

    [DataTestMethod]
    [DataRow("missing", 400)] [DataRow("invalid", 400)] [DataRow("anonymous", 401)] [DataRow("staff", 403)]
    public async Task AuthenticationAuthorizationAndAntiforgeryRemainInForce(string scenario, int status)
    {
        await using var host = await StaffHost.Start(role: scenario == "staff" ? "Staff" : "Administrator");
        using var beforeDb = new SignupContext(host.Options); var before = StaffInvitationData.Snapshot(beforeDb);
        using var result = scenario == "staff"
            ? await host.Send("POST", "/GarageTeam/Invite", "admin", StaffHost.InviteFields())
            : await host.Send("POST", "/GarageInvitation/Complete", scenario == "anonymous" ? null : "invitee",
                new() { ["token"] = StaffInvitationData.Token }, antiforgery: scenario);
        Assert.AreEqual(status, (int)result.StatusCode);
        using var check = new SignupContext(host.Options); Assert.AreEqual(before, StaffInvitationData.Snapshot(check));
    }

    [DataTestMethod]
    [DataRow("ResendInvitation","missing",400)] [DataRow("ResendInvitation","invalid",400)] [DataRow("ResendInvitation","anonymous",401)] [DataRow("ResendInvitation","staff",403)]
    [DataRow("RevokeInvitation","missing",400)] [DataRow("RevokeInvitation","invalid",400)] [DataRow("RevokeInvitation","anonymous",401)] [DataRow("RevokeInvitation","staff",403)]
    public async Task MutationFiltersRejectBeforeWrites(string action,string scenario,int status)
    {
        await using var host=await StaffHost.Start(role:scenario=="staff"?"Staff":"Administrator");
        using var initial=new SignupContext(host.Options);var before=StaffInvitationData.Snapshot(initial);
        using var response=await host.Send("POST","/GarageTeam/"+action,scenario=="anonymous"?null:"admin",new(){["invitationId"]="1"},antiforgery:scenario);
        Assert.AreEqual(status,(int)response.StatusCode);using var check=new SignupContext(host.Options);Assert.AreEqual(before,StaffInvitationData.Snapshot(check));Assert.AreEqual(0,host.Urls.Count);
    }
    [TestMethod]
    public async Task ConsumedInvitationCannotBeResentRevokedOrAcceptedAgain()
    {
        await using var host=await StaffHost.Start();
        using var accepted=await host.Send("POST","/GarageInvitation/Complete","invitee",new(){["token"]=StaffInvitationData.Token});Assert.AreEqual(HttpStatusCode.Redirect,accepted.StatusCode);
        foreach(var action in new[]{"ResendInvitation","RevokeInvitation"}){using var response=await host.Send("POST","/GarageTeam/"+action,"admin",new(){["invitationId"]="1"});Assert.AreEqual(HttpStatusCode.NotFound,response.StatusCode);}
        using var again=await host.Send("POST","/GarageInvitation/Complete","invitee",new(){["token"]=StaffInvitationData.Token});StringAssert.Contains(await again.Content.ReadAsStringAsync(),"This invitation is no longer valid");
        using var check=new SignupContext(host.Options);Assert.AreEqual(1,check.GarageBusinessUser.Count(x=>x.UserId==2));Assert.AreEqual(0,host.Urls.Count);
    }
    [TestMethod]
    public async Task PersistenceFailureReturnsSafeUnavailablePage()
    {
        await using var host=await StaffHost.Start(failSave:true);
        using var response=await host.Send("POST","/GarageInvitation/Complete","invitee",new(){["token"]=StaffInvitationData.Token});Assert.AreEqual(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        var body=await response.Content.ReadAsStringAsync();StringAssert.Contains(body,"could not be confirmed");Assert.IsFalse(body.Contains(StaffInvitationData.Token));
        using var check=new SignupContext(host.Options);Assert.IsFalse(check.GarageUserInvitation.Single().Accepted);Assert.AreEqual(0,check.GarageBusinessUser.Count(x=>x.UserId==2));
    }
    private sealed class HttpFaultContext(DbContextOptions<SignupContext> options):SignupContext(options)
    {public override int SaveChanges()=>throw new DbUpdateException("Injected HTTP persistence failure");}    private sealed class StaffHost : IAsyncDisposable
    {
        private IHost _host = null!; private HttpClient _client = null!;
        internal DbContextOptions<SignupContext> Options { get; private set; } = null!;
        internal readonly List<string> Urls = new();
        internal static Dictionary<string,string> InviteFields() => new() { ["FirstName"] = "New", ["LastName"] = "Employee", ["EmailAddress"] = "new@test.invalid", ["Role"] = "Staff", ["GarageBusinessId"] = "999" };
        internal static async Task<StaffHost> Start(string garage = "active", string? origin = "https://trusted.example", string userState = "active", string role = "Administrator", string invitation = "pending", bool failSave = false)
        {
            var instance = new StaffHost { Options = StaffInvitationData.Options() }; StaffInvitationData.Seed(instance.Options, garage, invitation: invitation, userState: userState, issuerRole: role);
            var email = StaffInvitationData.Email(instance.Urls).Object;
            instance._host = await new HostBuilder().ConfigureWebHost(web =>
            {
                web.UseKestrel(server => server.Listen(IPAddress.Loopback, 0));
                web.ConfigureServices(s =>
                {
                    s.AddLogging(); s.AddHttpContextAccessor(); s.AddScoped<SignupContext>(_ => failSave ? new HttpFaultContext(instance.Options) : new SignupContext(instance.Options));
                    s.AddScoped<ICurrentGarageUserService, CurrentGarageUserService>(); s.AddScoped<IGarageAuthorizationService, GarageAuthorizationService>();
                    s.AddScoped<IInvitationIdentityVerificationService, InvitationIdentityVerificationService>(); s.AddSingleton(email);
                    s.AddScoped(_ => new OnboardingInvitationService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
                        { ["Onboarding:PublicOrigin"] = origin }).Build(), email, NullLogger<OnboardingInvitationService>.Instance));
                    s.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, IdentityHandler>("Test", _ => { })
                        .AddScheme<AuthenticationSchemeOptions, IdentityHandler>(OpenIdConnectDefaults.AuthenticationScheme, _ => { });
                    s.AddAuthorization(o => o.AddPolicy("PlatformAdministrator", p => p.RequireAssertion(_ => false)));
                    s.AddControllersWithViews().AddApplicationPart(typeof(GarageInvitationController).Assembly);
                });
                web.Configure(app =>
                {
                    app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseMiddleware<GarageAccessMiddleware>();
                    app.UseEndpoints(routes =>
                    {
                        routes.MapGet("/test/token", async c => await c.Response.WriteAsync(c.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(c).RequestToken!));
                        routes.MapGet("/test/current", c => { c.RequestServices.GetRequiredService<ICurrentGarageUserService>().GetCurrentUser(); return Task.CompletedTask; });
                        routes.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
                    });
                });
            }).StartAsync();
            instance._client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                { BaseAddress = new Uri(instance._host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            return instance;
        }
        internal async Task<HttpResponseMessage> Send(string method, string path, string? identity, Dictionary<string,string>? fields = null, bool forgedHost = false, string antiforgery = "valid")
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (identity != null) request.Headers.Add("X-Test-Identity", identity);
            if (forgedHost) request.Headers.Host = "attacker.example";
            if (fields != null)
            {
                request.Content = new FormUrlEncodedContent(fields);
                if (antiforgery != "missing")
                {
                    using var get = new HttpRequestMessage(HttpMethod.Get, "/test/token");
                    if (identity != null) get.Headers.Add("X-Test-Identity", identity);
                    using var response = await _client.SendAsync(get);
                    request.Headers.Add("RequestVerificationToken", antiforgery == "invalid" ? "forged" : await response.Content.ReadAsStringAsync());
                }
            }
            return await _client.SendAsync(request);
        }
        public async ValueTask DisposeAsync() { _client?.Dispose(); if (_host != null) { await _host.StopAsync(); _host.Dispose(); } }
    }
    private sealed class IdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var id = Request.Headers["X-Test-Identity"].ToString();
            if (string.IsNullOrEmpty(id)) return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[]
            { new Claim(ClaimTypes.NameIdentifier, id), new Claim("emails", id + "@test.invalid") }, "Test")), Scheme.Name)));
        }
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            if (Scheme.Name == OpenIdConnectDefaults.AuthenticationScheme) { Response.StatusCode = 302; Response.Headers.Location = "/test-b2c"; return Task.CompletedTask; }
            return base.HandleChallengeAsync(properties);
        }
    }
}

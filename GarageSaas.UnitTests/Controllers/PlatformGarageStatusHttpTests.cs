using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GarageSaas.Authorization;
using GarageSaas.Controllers;
using GarageSaas.Middleware;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class PlatformGarageStatusHttpTests
{
    [DataTestMethod]
    [DataRow("anonymous",401)] [DataRow("Owner",403)] [DataRow("Administrator",403)] [DataRow("Staff",403)]
    [DataRow("inactive-platform",403)] [DataRow("blocked-platform",403)]
    public async Task RealPolicyDeniesNonPlatformStatusAccess(string role,int status)
    {
        await using var host=await StatusHost.Start(role);var identity=role=="anonymous"?null:"admin";
        foreach(var action in new[]{"Activate","Deactivate"})
        {
            using var get=await host.Send("GET","/PlatformAdmin/"+action+"?id=1",identity);Assert.AreEqual(status,(int)get.StatusCode);
            using var post=await host.Send("POST","/PlatformAdmin/"+action,identity,Fields(false));Assert.AreEqual(status,(int)post.StatusCode);
        }
        using var check=new SignupContext(host.Options);Assert.IsFalse(check.GarageBusiness.Single().Active);Assert.IsNull(check.GarageBusiness.Single().UpdatedDate);
    }
    [DataTestMethod] [DataRow("missing")] [DataRow("invalid")]
    public async Task AntiforgeryRejectsBothMutationsBeforeWrites(string token)
    {
        await using var host=await StatusHost.Start();
        foreach(var action in new[]{"Activate","Deactivate"}){using var result=await host.Send("POST","/PlatformAdmin/"+action,"admin",Fields(false),token);Assert.AreEqual(HttpStatusCode.BadRequest,result.StatusCode);}
        using var check=new SignupContext(host.Options);Assert.IsFalse(check.GarageBusiness.Single().Active);Assert.IsNull(check.GarageBusiness.Single().UpdatedDate);
    }
    [TestMethod]
    public async Task ConfirmationCancelAndPlatformOnlyActivationPreserveDisabledState()
    {
        await using var host=await StatusHost.Start(blocked:true);
        using var confirmation=await host.Send("GET","/PlatformAdmin/Activate?id=1","admin");Assert.AreEqual(HttpStatusCode.OK,confirmation.StatusCode);
        var html=await confirmation.Content.ReadAsStringAsync();StringAssert.Contains(html,"Confirm activation");StringAssert.Contains(html,"Cancel");StringAssert.Contains(html,"__RequestVerificationToken");
        using var cancel=await host.Send("GET","/PlatformAdmin/GarageDetails/1","admin");Assert.AreEqual(HttpStatusCode.OK,cancel.StatusCode);
        using(var check=new SignupContext(host.Options)){Assert.IsFalse(check.GarageBusiness.Single().Active);Assert.IsNull(check.GarageBusiness.Single().UpdatedDate);Assert.IsFalse(check.GarageBusinessUser.Any(x=>x.UserId==1));}
        var fields=Fields(false,true);fields["Blocked"]="false";fields["Activate"]="false";fields["UserActive"]="true";
        using var post=await host.Send("POST","/PlatformAdmin/Activate","admin",fields);Assert.AreEqual(HttpStatusCode.Redirect,post.StatusCode);
        using var saved=new SignupContext(host.Options);Assert.IsTrue(saved.GarageBusiness.Single().Active);Assert.IsTrue(saved.GarageBusiness.Single().Blocked);
        Assert.IsFalse(saved.Users.Single(x=>x.Id==2).Active);Assert.IsFalse(saved.GarageBusinessUser.Single(x=>x.UserId==2).IsActive);
        using var again=await host.Send("GET","/PlatformAdmin/GarageDetails/1","admin");Assert.AreEqual(HttpStatusCode.OK,again.StatusCode);
    }
    [TestMethod]
    public async Task DeactivationDeniesNextTenantRequestAndStalePostReturns409()
    {
        await using var host=await StatusHost.Start(active:true,eligibleMember:true);
        using var before=await host.Send("GET","/test/current","invitee");Assert.AreEqual(HttpStatusCode.OK,before.StatusCode);
        using var confirmed=await host.Send("GET","/PlatformAdmin/Deactivate?id=1","admin");Assert.AreEqual(HttpStatusCode.OK,confirmed.StatusCode);
        using var post=await host.Send("POST","/PlatformAdmin/Deactivate","admin",Fields(true));Assert.AreEqual(HttpStatusCode.Redirect,post.StatusCode);
        using var after=await host.Send("GET","/test/current","invitee");Assert.AreEqual(HttpStatusCode.Forbidden,after.StatusCode);
        using var stale=await host.Send("POST","/PlatformAdmin/Deactivate","admin",Fields(true));Assert.AreEqual(HttpStatusCode.Conflict,stale.StatusCode);
        StringAssert.Contains(await stale.Content.ReadAsStringAsync(),"changed after confirmation");
        using var platform=await host.Send("GET","/PlatformAdmin/Garages","admin");Assert.AreEqual(HttpStatusCode.OK,platform.StatusCode);
    }
    private static Dictionary<string,string> Fields(bool active,bool blocked=false)=>new(){["Id"]="1",["ExpectedActive"]=active.ToString(),["ExpectedBlocked"]=blocked.ToString(),["ExpectedUpdatedDate"]="none"};
    private sealed class StatusHost:IAsyncDisposable
    {
        private IHost _host=null!;private HttpClient _client=null!;
        internal DbContextOptions<SignupContext> Options {get;private set;}=null!;
        internal static async Task<StatusHost> Start(string role="platform",bool active=false,bool blocked=false,bool eligibleMember=false)
        {
            var h=new StatusHost{Options=StaffInvitationData.Options()};StaffInvitationData.Seed(h.Options,userState:eligibleMember?"existing-membership":"inactive-membership",issuerRole:role);
            using(var db=new SignupContext(h.Options))
            {
                var g=db.GarageBusiness.Single();g.Active=active;g.Blocked=blocked;
                var a=db.Users.Single(x=>x.Id==1);a.IsPlatformAdministrator=role.Contains("platform");a.Active=role!="inactive-platform";a.Blocked=role=="blocked-platform";
                if(a.IsPlatformAdministrator)db.Remove(db.GarageBusinessUser.Single(x=>x.UserId==1));
                if(!eligibleMember)db.Users.Single(x=>x.Id==2).Active=false;db.SaveChanges();
            }
            h._host=await new HostBuilder().ConfigureWebHost(web=>{
                web.UseKestrel(s=>s.Listen(IPAddress.Loopback,0));
                web.ConfigureServices(s=>{
                    s.AddLogging();s.AddHttpContextAccessor();s.AddScoped(_=>new SignupContext(h.Options));
                    s.AddScoped<ICurrentGarageUserService,CurrentGarageUserService>();s.AddScoped<IGarageAuthorizationService,GarageAuthorizationService>();
                    s.AddScoped<IAuthorizationHandler,PlatformAdministratorHandler>();
                    s.AddAuthorization(o=>o.AddPolicy("PlatformAdministrator",p=>p.RequireAuthenticatedUser().AddRequirements(new PlatformAdministratorRequirement())));
                    s.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions,IdentityHandler>("Test",_=>{});
                    s.AddSingleton(Mock.Of<IRegistrationRequestLock>());s.AddScoped(_=>new OnboardingInvitationService(new ConfigurationBuilder().Build(),Mock.Of<IEmailService>(),NullLogger<OnboardingInvitationService>.Instance));
                    s.AddControllersWithViews().AddApplicationPart(typeof(PlatformAdminController).Assembly);
                });
                web.Configure(app=>{app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.UseMiddleware<GarageAccessMiddleware>();app.UseEndpoints(r=>{
                    r.MapGet("/test/token",async c=>await c.Response.WriteAsync(c.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(c).RequestToken!));
                    r.MapGet("/test/current",c=>{c.RequestServices.GetRequiredService<ICurrentGarageUserService>().GetCurrentUser();return Task.CompletedTask;});
                    r.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
                });});
            }).StartAsync();
            h._client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new Uri(h._host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};return h;
        }
        internal async Task<HttpResponseMessage> Send(string method,string path,string? identity,Dictionary<string,string>? fields=null,string token="valid")
        {
            using var request=new HttpRequestMessage(new HttpMethod(method),path);if(identity!=null)request.Headers.Add("X-Test-Identity",identity);
            if(fields!=null){request.Content=new FormUrlEncodedContent(fields);if(token!="missing"){
                using var get=new HttpRequestMessage(HttpMethod.Get,"/test/token");if(identity!=null)get.Headers.Add("X-Test-Identity",identity);
                using var response=await _client.SendAsync(get);request.Headers.Add("RequestVerificationToken",token=="invalid"?"forged":await response.Content.ReadAsStringAsync());}}
            return await _client.SendAsync(request);
        }
        public async ValueTask DisposeAsync(){_client?.Dispose();if(_host!=null){await _host.StopAsync();_host.Dispose();}}
    }
    private sealed class IdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var id=Request.Headers["X-Test-Identity"].ToString();return Task.FromResult(string.IsNullOrEmpty(id)?AuthenticateResult.NoResult():AuthenticateResult.Success(new AuthenticationTicket(StaffInvitationData.Http(id).User,Scheme.Name)));
        }
    }
}

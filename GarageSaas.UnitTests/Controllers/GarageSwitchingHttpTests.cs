using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using GarageSaas.Authorization;
using GarageSaas.Controllers;
using GarageSaas.Filters;
using GarageSaas.Middleware;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
public class GarageSwitchingHttpTests
{
    [TestMethod]
    public async Task SwitchRememberRolesAndTenantReadsUseSelectedGarage()
    {
        await using var h=await SwitchHost.Start();
        using var absent=await h.Send("GET","/GarageCustomers/GarageCustomersList");Assert.AreEqual(HttpStatusCode.Forbidden,absent.StatusCode);
        using var chooser=await h.Send("GET","/GarageSelection");var choices=await chooser.Content.ReadAsStringAsync();Assert.AreEqual(HttpStatusCode.OK,chooser.StatusCode);StringAssert.Contains(choices,"Garage One");StringAssert.Contains(choices,"Garage Two");Assert.IsFalse(choices.Contains("Unavailable Garage"));
        await h.Select(1);
        using var one=await h.Send("GET","/GarageCustomers/GarageCustomersList");var a=await one.Content.ReadAsStringAsync();Assert.AreEqual(HttpStatusCode.OK,one.StatusCode);StringAssert.Contains(a,"CustomerAlpha");Assert.IsFalse(a.Contains("CustomerBeta"));StringAssert.Contains(a,"Garage One");Assert.IsTrue(one.Headers.CacheControl!.NoStore);
        using var teamOne=await h.Send("GET","/GarageTeam/Index");Assert.AreEqual(HttpStatusCode.OK,teamOne.StatusCode);
        await h.Select(2);
        using var two=await h.Send("GET","/GarageCustomers/GarageCustomersList");var b=await two.Content.ReadAsStringAsync();Assert.AreEqual(HttpStatusCode.OK,two.StatusCode);StringAssert.Contains(b,"CustomerBeta");Assert.IsFalse(b.Contains("CustomerAlpha"));StringAssert.Contains(b,"Garage Two");
        using var forbidden=await h.Send("GET","/GarageCustomers/EditGarageCustomer?garageCustomerId=1");Assert.AreEqual(HttpStatusCode.Redirect,forbidden.StatusCode);Assert.AreEqual("/GarageCustomers/GarageCustomersList",forbidden.Headers.Location!.OriginalString);Assert.IsFalse((await forbidden.Content.ReadAsStringAsync()).Contains("CustomerAlpha"));
        using var teamTwo=await h.Send("GET","/GarageTeam/Index");Assert.AreEqual(HttpStatusCode.Forbidden,teamTwo.StatusCode);
        // New authenticated requests restore the same protected preference, with no selection in the URL.
        using var restored=await h.Send("GET","/");Assert.AreEqual(HttpStatusCode.OK,restored.StatusCode);StringAssert.Contains(await restored.Content.ReadAsStringAsync(),"Garage Two");
        using var platform=await h.Send("GET","/PlatformAdmin/Garages",identity:"platform");Assert.AreEqual(HttpStatusCode.OK,platform.StatusCode);
    }
    [DataTestMethod] [DataRow(999)] [DataRow(3)]
    public async Task UnauthorisedOrUnavailableSelectionDoesNotReplacePreference(int garage)
    {
        await using var h=await SwitchHost.Start();await h.Select(1);var cookie=h.Preference;
        using var rejected=await h.Send("POST","/GarageSelection/Select",new(){["garageId"]=garage.ToString(),["returnUrl"]="https://evil.invalid"});Assert.AreEqual(HttpStatusCode.Forbidden,rejected.StatusCode);Assert.AreEqual(cookie,h.Preference);
    }
    [DataTestMethod] [DataRow("missing")] [DataRow("invalid")] [DataRow("anonymous")]
    public async Task SelectionRequiresAuthenticationAndAntiforgery(string state)
    {
        await using var h=await SwitchHost.Start();using var response=await h.Send("POST","/GarageSelection/Select",new(){["garageId"]="1"},identity:state=="anonymous"?null:"member",antiforgery:state);
        Assert.AreEqual(state=="anonymous"?HttpStatusCode.Unauthorized:HttpStatusCode.BadRequest,response.StatusCode);Assert.IsNull(h.Preference);
    }
    [DataTestMethod] [DataRow("membership")] [DataRow("deactivated")] [DataRow("blocked")]
    public async Task RememberedGarageBecomingUnavailableRequiresAnotherExplicitChoice(string kind)
    {
        await using var h=await SwitchHost.Start();await h.Select(2);
        using(var db=new SignupContext(h.Options)){if(kind=="membership")db.GarageBusinessUser.Single(x=>x.Id==2).IsActive=false;else{var g=db.GarageBusiness.Single(x=>x.Id==2);g.Active=kind!="deactivated";g.Blocked=kind=="blocked";}db.SaveChanges();}
        using var denied=await h.Send("GET","/GarageCustomers/GarageCustomersList");Assert.AreEqual(HttpStatusCode.Forbidden,denied.StatusCode);
        using var chooser=await h.Send("GET","/GarageSelection");var html=await chooser.Content.ReadAsStringAsync();StringAssert.Contains(html,"Garage One");Assert.IsFalse(html.Contains("<h3 class=\"h5\">Garage Two"));
        await h.Select(1);using var allowed=await h.Send("GET","/GarageCustomers/GarageCustomersList");Assert.AreEqual(HttpStatusCode.OK,allowed.StatusCode);
    }
    [TestMethod]
    public async Task StaleFormsAndAjaxCannotMutateNewGarage()
    {
        await using var h=await SwitchHost.Start();await h.Select(1);
        using var form=await h.Send("GET","/GarageBusiness/EditGarageBusiness");var html=await form.Content.ReadAsStringAsync();Assert.AreEqual(HttpStatusCode.OK,form.StatusCode);StringAssert.Contains(html,"name=\"__GarageContext\"");
        using var nativeForm=await h.Send("GET","/GarageTeam/Invite");Assert.AreEqual(HttpStatusCode.OK,nativeForm.StatusCode);StringAssert.Contains(await nativeForm.Content.ReadAsStringAsync(),"name=\"__GarageContext\"");
        var formToken=Regex.Match(html,"name=\"__GarageContext\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;Assert.IsFalse(string.IsNullOrEmpty(formToken));
        var pageToken=Regex.Match(html,"name=\"garage-context\" content=\"([^\"]+)\"").Groups[1].Value;
        // A current, authorized AJAX mutation works with the page context.
        using var current=await h.Send("POST","/GarageTeam/Invite",StaffHostFields(),context:pageToken);Assert.AreEqual(HttpStatusCode.Redirect,current.StatusCode);
        await h.Select(2);
        using var stale=await h.Send("POST","/GarageBusiness/UpdateGarageBusiness",new(){["Id"]="2",["GarageBusinessName"]="Injected",["__GarageContext"]=WebUtility.HtmlDecode(formToken)});Assert.AreEqual(HttpStatusCode.Conflict,stale.StatusCode);
        using var staleAjax=await h.Send("POST","/GarageTeam/Invite",StaffHostFields(),context:pageToken);Assert.AreEqual(HttpStatusCode.Conflict,staleAjax.StatusCode);
        using var staleRead=await h.Send("GET","/GarageCustomers/GarageCustomersList",context:pageToken);Assert.AreEqual(HttpStatusCode.Conflict,staleRead.StatusCode);
        using var missing=await h.Send("POST","/GarageTeam/Invite",StaffHostFields());Assert.AreEqual(HttpStatusCode.Conflict,missing.StatusCode);
        using var check=new SignupContext(h.Options);Assert.IsFalse(check.GarageBusiness.Any(x=>x.GarageBusinessName=="Injected"));Assert.AreEqual(1,check.GarageUserInvitation.Count(x=>x.EmailAddress=="new@test.invalid"));
    }
    [TestMethod]
    public async Task SingleGarageLoginAndInvitationRoutesRemainUsable()
    {
        await using var h=await SwitchHost.Start(single:true);
        using var dashboard=await h.Send("GET","/");Assert.AreEqual(HttpStatusCode.OK,dashboard.StatusCode);Assert.IsNotNull(h.Preference);
        using var publicInvite=await h.Send("GET","/GarageInvitation/Accept?token="+StaffInvitationData.Token,identity:null);Assert.AreEqual(HttpStatusCode.OK,publicInvite.StatusCode);
        using var accepted=await h.Send("POST","/GarageInvitation/Complete",new(){["token"]=StaffInvitationData.Token},identity:"invitee");Assert.AreEqual(HttpStatusCode.Redirect,accepted.StatusCode);
        using var joined=await h.Send("GET","/",identity:"invitee");Assert.AreEqual(HttpStatusCode.OK,joined.StatusCode);
        using var retired=await h.Send("POST","/VehiclePart/AddVehiclePart");Assert.AreEqual(HttpStatusCode.Gone,retired.StatusCode);
    }
    [TestMethod]
    public async Task SecondGarageInvitationReturnReachesChooserThenSelectedDashboard()
    {
        await using var h=await SwitchHost.Start(single:true);
        using(var db=new SignupContext(h.Options))
        {
            // Real pending invitation to the member's second garage, issued by its administrator.
            db.Add(new GarageBusinessUser{Id=4,UserId=2,GarageBusinessId=2,IsActive=true,Role="Administrator"});
            var invite=db.GarageUserInvitation.Single();invite.GarageBusinessId=2;invite.InvitedByUserId=2;invite.EmailAddress="member@test.invalid";db.SaveChanges();
        }
        using var accepted=await h.Send("POST","/GarageInvitation/Complete",new(){["token"]=StaffInvitationData.Token});
        Assert.AreEqual(HttpStatusCode.Redirect,accepted.StatusCode);Assert.IsNull(h.Preference);
        using var returned=await h.Send("GET",accepted.Headers.Location!.OriginalString,accept:"text/html");
        Assert.AreEqual(HttpStatusCode.Redirect,returned.StatusCode);Assert.AreEqual("/GarageSelection",returned.Headers.Location!.OriginalString);
        using var chooser=await h.Send("GET",returned.Headers.Location.OriginalString,accept:"text/html");
        Assert.AreEqual(HttpStatusCode.OK,chooser.StatusCode);Assert.IsNull(chooser.Headers.Location);
        var html=await chooser.Content.ReadAsStringAsync();StringAssert.Contains(html,"Garage One");StringAssert.Contains(html,"Garage Two");
        await h.Select(2);
        using var dashboard=await h.Send("GET","/",accept:"text/html");Assert.AreEqual(HttpStatusCode.OK,dashboard.StatusCode);StringAssert.Contains(await dashboard.Content.ReadAsStringAsync(),"Garage Two");
        using var check=new SignupContext(h.Options);Assert.AreEqual(2,check.GarageBusinessUser.Count(x=>x.UserId==1));Assert.IsTrue(check.GarageUserInvitation.Single().Accepted);
    }    private static Dictionary<string,string> StaffHostFields()=>new(){["EmailAddress"]="new@test.invalid",["FirstName"]="New",["LastName"]="Staff",["Role"]="Staff"};

    private sealed class SwitchHost:IAsyncDisposable
    {
        private IHost _host=null!;private HttpClient _client=null!;
        internal DbContextOptions<SignupContext> Options {get;private set;}=null!;
        internal string? Preference;
        internal static async Task<SwitchHost> Start(bool single=false)
        {
            var h=new SwitchHost{Options=StaffInvitationData.Options()};StaffInvitationData.Seed(h.Options);
            using(var db=new SignupContext(h.Options))
            {
                var member=db.Users.Single(x=>x.Id==1);member.ExternalUserId="member";db.GarageBusiness.Single().GarageBusinessName="Garage One";
                var m=db.GarageBusinessUser.Single();m.IsOwner=true;m.Role="Owner";
                db.Add(new GarageBusiness{Id=2,GarageBusinessName="Garage Two",Active=true});db.Add(new GarageBusiness{Id=3,GarageBusinessName="Unavailable Garage",Active=false});
                if(!single)db.Add(new GarageBusinessUser{Id=2,UserId=1,GarageBusinessId=2,IsActive=true,Role="Staff"});
                db.Add(new GarageBusinessUser{Id=3,UserId=1,GarageBusinessId=3,IsActive=false,Role="Staff"});
                if(single)db.Remove(db.GarageBusinessUser.Local.Single(x=>x.Id==3));
                db.Add(new Users{Id=3,ExternalUserId="platform",Active=true,IsPlatformAdministrator=true});
                db.AddRange(new GarageBusinessCustomer{Id=1,GarageBusinessId=1,GarageCustomerForename="CustomerAlpha",GarageCustomerSurname="OnlyA",Active=true},new GarageBusinessCustomer{Id=2,GarageBusinessId=2,GarageCustomerForename="CustomerBeta",GarageCustomerSurname="OnlyB",Active=true});db.SaveChanges();
            }
            h._host=await new HostBuilder().ConfigureWebHost(web=>{
                web.UseKestrel(s=>s.Listen(IPAddress.Loopback,0));web.ConfigureServices(s=>{
                    s.AddLogging();s.AddHttpContextAccessor();s.AddDataProtection();s.AddScoped(_=>new SignupContext(h.Options));s.AddScoped<GarageSelectionService>();s.AddScoped<GarageContextFilter>();
                    s.AddScoped<ICurrentGarageUserService,CurrentGarageUserService>();s.AddScoped<IGarageAuthorizationService,GarageAuthorizationService>();
                    s.AddScoped<IGarageCustomersService,GarageCustomersService>();s.AddScoped<IGarageBusinessService,GarageBusinessService>();s.AddSingleton(Mock.Of<IVehicleLookupService>());
                    s.AddSingleton(Mock.Of<IEmailService>());s.AddSingleton(Mock.Of<IRegistrationRequestLock>());s.AddScoped<IInvitationIdentityVerificationService,InvitationIdentityVerificationService>();
                    s.AddScoped(_=>new OnboardingInvitationService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Onboarding:PublicOrigin"]="https://trusted.example"}).Build(),Mock.Of<IEmailService>(),NullLogger<OnboardingInvitationService>.Instance));
                    s.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,PlatformAdministratorHandler>();
                    s.AddAuthorization(o=>o.AddPolicy("PlatformAdministrator",p=>p.RequireAuthenticatedUser().AddRequirements(new PlatformAdministratorRequirement())));
                    s.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions,IdentityHandler>("Test",_=>{});
                    s.AddControllersWithViews(o=>o.Filters.AddService<GarageContextFilter>()).AddApplicationPart(typeof(GarageSelectionController).Assembly);
                });web.Configure(app=>{app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.UseMiddleware<GarageAccessMiddleware>();app.UseEndpoints(r=>{
                    r.MapGet("/test/token",async c=>await c.Response.WriteAsync(c.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(c).RequestToken!));
                    r.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");});});
            }).StartAsync();
            h._client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){BaseAddress=new Uri(h._host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};return h;
        }
        private string? _antiforgeryCookie;
        internal async Task Select(int garage)
        {using var response=await Send("POST","/GarageSelection/Select",new(){["garageId"]=garage.ToString()});Assert.AreEqual(HttpStatusCode.Redirect,response.StatusCode);Assert.AreEqual("/",response.Headers.Location!.OriginalString);}
        internal async Task<HttpResponseMessage> Send(string method,string path,Dictionary<string,string>? fields=null,string? identity="member",string antiforgery="valid",string? context=null,string? accept=null)
        {
            using var request=new HttpRequestMessage(new HttpMethod(method),path);if(identity!=null)request.Headers.Add("X-Test-Identity",identity);
            if(fields!=null)
            {
                if(antiforgery!="missing"){
                    using var token=await Send("GET","/test/token",identity:identity);request.Headers.Add("RequestVerificationToken",antiforgery=="invalid"?"forged":await token.Content.ReadAsStringAsync());}
                request.Content=new FormUrlEncodedContent(fields);
            }
            if(accept!=null)request.Headers.Accept.ParseAdd(accept);
            if(context!=null)request.Headers.Add("X-Garage-Context",WebUtility.HtmlDecode(context));
            var cookies=new[]{Preference,_antiforgeryCookie}.Where(x=>x!=null);if(cookies.Any())request.Headers.Add("Cookie",string.Join("; ",cookies));
            var response=await _client.SendAsync(request);
            if(response.Headers.TryGetValues("Set-Cookie",out var values))foreach(var value in values){var cookie=value.Split(';')[0];if(cookie.StartsWith(GarageSelectionService.CookieName+"="))Preference=cookie;else if(cookie.StartsWith(".AspNetCore.Antiforgery"))_antiforgeryCookie=cookie;}
            return response;
        }
        public async ValueTask DisposeAsync(){_client?.Dispose();if(_host!=null){await _host.StopAsync();_host.Dispose();}}
    }
    private sealed class IdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {var id=Request.Headers["X-Test-Identity"].ToString();return Task.FromResult(string.IsNullOrEmpty(id)?AuthenticateResult.NoResult():AuthenticateResult.Success(new AuthenticationTicket(StaffInvitationData.Http(id).User,Scheme.Name)));}
    }
}

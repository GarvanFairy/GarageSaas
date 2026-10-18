using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GarageSaas.Controllers;
using GarageSaas.Models;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using GarageSaas.UnitTests.TestHelpers;
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
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Controllers;

[TestClass, DoNotParallelize]
public class CustomerVehicleAntiforgeryHttpTests
{
    [DataTestMethod]
    [DataRow("valid")] [DataRow("missing")] [DataRow("invalid")] [DataRow("cookie-missing")] [DataRow("service-error")]
    public async Task MvcAntiforgeryRunsBeforeServiceMutationAndPreservesValidEnvelope(string mode)
    {
        var options = new DbContextOptionsBuilder<SignupContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        int garage;
        using (var db = new SignupContext(options))
        {
            var graph = SqlInvoiceGraph.Seed(db); garage = graph.GarageId;
            var user = new Users { ExternalUserId = "member", Active = true, FirstName = "HTTP", LastName = "Member" };
            db.Add(user); db.SaveChanges(); db.Add(new GarageBusinessUser { UserId = user.Id, GarageBusinessId = garage, IsActive = true, Role = "Staff" }); db.SaveChanges();
        }
        var service = new Mock<ICustomerVehicleService>(MockBehavior.Strict); var calls = 0;
        service.Setup(x => x.AddOrUpdateCustomerVehicle(It.IsAny<VehicleAndCustomers>(), garage, It.IsAny<string>()))
            .Returns(() => { calls++; return mode == "service-error" ? ServiceResult.Fail("Safe validation failure") : ServiceResult.Ok(); });
        using var host = await new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseKestrel(server => server.Listen(IPAddress.Loopback, 0));
            web.ConfigureServices(s =>
            {
                s.AddLogging(); s.AddHttpContextAccessor(); s.AddScoped(_ => new SignupContext(options));
                s.AddScoped<ICurrentGarageUserService, CurrentGarageUserService>(); s.AddSingleton(service.Object);
                s.AddSingleton(Mock.Of<IVehicleLookupService>());
                s.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, IdentityHandler>("Test", _ => { });
                s.AddAuthorization(); s.AddControllersWithViews().AddApplicationPart(typeof(CustomerVehicleController).Assembly);
            });
            web.Configure(app =>
            {
                app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
                app.UseEndpoints(routes =>
                {
                    // Test-host-only endpoint issues genuine ASP.NET Core cookie/request token pairs.
                    routes.MapGet("/test/token", async context => await context.Response.WriteAsync(context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context).RequestToken!));
                    routes.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
                });
            });
        }).StartAsync();
        try
        {
            var address = new Uri(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            using var client = new HttpClient(new HttpClientHandler { UseCookies = mode != "cookie-missing", AllowAutoRedirect = false }) { BaseAddress = address };
            client.DefaultRequestHeaders.Add("X-Test-Identity", "member");
            var token = await client.GetStringAsync("/test/token");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/CustomerVehicle/AddUpdateCustomerVehicle")
            { Content = JsonContent.Create(OwnershipCases.Input(0, "0")) };
            if (mode != "missing") request.Headers.Add("RequestVerificationToken", mode == "invalid" ? "forged" : token);
            using var response = await client.SendAsync(request);
            var valid = mode is "valid" or "service-error";
            Assert.AreEqual(valid ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(valid ? 1 : 0, calls);
            if (mode == "valid") Assert.AreEqual("\"Success\"", await response.Content.ReadAsStringAsync());
            if (mode == "service-error") StringAssert.Contains(await response.Content.ReadAsStringAsync(), "\"status\":\"Error\"");
        }
        finally { await host.StopAsync(); }
    }
    private sealed class IdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = Request.Headers["X-Test-Identity"].ToString();
            if (string.IsNullOrEmpty(identity)) return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, identity) }, "Test")), "Test")));
        }
    }
}


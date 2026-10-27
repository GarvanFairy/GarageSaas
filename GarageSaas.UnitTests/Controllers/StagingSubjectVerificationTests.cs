using System.Security.Claims;
using GarageSaas.Middleware;
using GarageSaas.UnitTests.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class StagingSubjectVerificationTests
{
    private const string Subject = "11111111-2222-3333-4444-555555555555";
    private const string ObjectId = "64c45703-ef4f-4119-a4a5-addff7909b69";

    [DataTestMethod]
    [DataRow("approved", true)]
    [DataRow("mapped-oid", true)]
    [DataRow("disabled", false)]
    [DataRow("absent-flag", false)]
    [DataRow("anonymous", false)]
    [DataRow("other-account", false)]
    [DataRow("email-only", false)]
    [DataRow("conflicting-oid", false)]
    [DataRow("wrong-host", false)]
    [DataRow("host-lookalike", false)]
    [DataRow("wrong-site", false)]
    [DataRow("wrong-environment", false)]
    [DataRow("http", false)]
    [DataRow("wrong-path", false)]
    [DataRow("missing-subject", false)]
    [DataRow("multiple-subjects", false)]
    [DataRow("different-request-principal", false)]
    [DataRow("failed-page", false)]
    [DataRow("public-home", false)]
    public async Task OnlyApprovedValidatedCookieCanCreatePrivateSubjectRecord(string scenario, bool expected)
    {
        var home = Path.Combine(Path.GetTempPath(), "GarageSaas-subject-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = ProductionConfigurationTests.Settings();
            settings["StagingVerification:CaptureOwnSubject"] = scenario == "disabled" ? "false" : "true";
            if (scenario == "absent-flag") settings.Remove("StagingVerification:CaptureOwnSubject");
            settings["WEBSITE_SITE_NAME"] = scenario == "wrong-site" ? "another-app" : "garagesaas-stg-d49b2f61-weu";
            settings["HOME"] = home;
            var environment = new ProductionConfigurationTests.Environment(scenario == "wrong-environment" ? "Development" : "Production")
            { WebRootPath = scenario == "public-home" ? home : Path.Combine(home, "site", "wwwroot") };
            var claims = new List<Claim>
            {
                new(ClaimTypes.Email, "ggallagher116@hotmail.com"),
                new("unrelated-claim", "sentinel-unrelated"),
                new("access_token", "sentinel-token")
            };
            if (scenario != "missing-subject") claims.Add(new(ClaimTypes.NameIdentifier, Subject));
            if (scenario == "multiple-subjects") claims.Add(new(ClaimTypes.NameIdentifier, "another-subject"));
            if (scenario != "email-only") claims.Add(new(scenario == "mapped-oid" ? "http://schemas.microsoft.com/identity/claims/objectidentifier" : "oid", scenario == "other-account" ? "other-object" : ObjectId));
            if (scenario == "conflicting-oid") claims.Add(new("oid", "other-object"));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, scenario == "anonymous" ? null : "validated-cookie"));
            var authentication = new Mock<IAuthenticationService>();
            authentication.Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), CookieAuthenticationDefaults.AuthenticationScheme))
                .ReturnsAsync(scenario == "anonymous" ? AuthenticateResult.NoResult() : AuthenticateResult.Success(new AuthenticationTicket(principal, CookieAuthenticationDefaults.AuthenticationScheme)));
            using var services = new ServiceCollection().AddSingleton(authentication.Object).BuildServiceProvider();
            var context = new DefaultHttpContext { RequestServices = services, User = principal };
            if (scenario == "different-request-principal") context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "different") }, "other"));
            context.Request.Scheme = scenario == "http" ? "http" : "https";
            context.Request.Method = "GET";
            context.Request.Path = scenario == "wrong-path" ? "/health" : "/Home/NoGarageAccess";
            context.Request.Host = new HostString(scenario switch
            {
                "wrong-host" => "other.azurewebsites.net",
                "host-lookalike" => "garagesaas-stg-d49b2f61-weu.azurewebsites.net.attacker.invalid",
                _ => "garagesaas-stg-d49b2f61-weu.azurewebsites.net"
            });
            context.Request.Headers.Cookie = "sentinel-cookie";
            context.Response.Body = new MemoryStream();
            var middleware = new StagingSubjectVerificationMiddleware(async c =>
            {
                c.Response.StatusCode = scenario == "failed-page" ? 403 : 200;
                await c.Response.WriteAsync("normal page");
            }, ProductionConfigurationTests.Config(settings), environment);
            await middleware.InvokeAsync(context);
            var path = Path.Combine(home, "data", "staging-identity-verification", "nameidentifier.txt");
            Assert.AreEqual(expected, File.Exists(path), scenario);
            if (expected)
            {
                Assert.AreEqual(Subject, await File.ReadAllTextAsync(path));
                // Capture cannot replace an existing record.
                await File.WriteAllTextAsync(path, "existing-private-record");
                await middleware.InvokeAsync(context);
                Assert.AreEqual("existing-private-record", await File.ReadAllTextAsync(path));
                Assert.AreEqual(1, Directory.GetFiles(home, "*", SearchOption.AllDirectories).Length);
            }
            context.Response.Body.Position = 0;
            var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
            Assert.IsFalse(body.Contains(Subject));
            Assert.IsFalse(body.Contains("sentinel"));
        }
        finally { if (Directory.Exists(home)) Directory.Delete(home, true); }
    }
}

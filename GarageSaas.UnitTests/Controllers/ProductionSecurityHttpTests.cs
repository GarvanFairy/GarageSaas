using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using GarageSaas.UnitTests.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class ProductionSecurityHttpTests
{
    [TestMethod]
    public async Task ProductionPipelineProtectsCookiesMutationsAndErrorsWithoutLiveServices()
    {
        var settings = ProductionConfigurationTests.Settings();
        var logoDirectory = Path.Combine(Path.GetTempPath(), "GarageSaas-staging-logos-" + Guid.NewGuid().ToString("N"));
        settings["Storage:GarageLogoDirectory"] = logoDirectory;
        settings["AzureAdB2C:CallbackPath"] = "/signin-oidc";
        settings["AzureAdB2C:SignedOutCallbackPath"] = "/signout-callback-oidc";
        var environment = new ProductionConfigurationTests.Environment("Production");
        var startup = new GarageSaas.Startup(ProductionConfigurationTests.Config(settings), environment);
        var logs = new Logs();
        using var host = Host.CreateDefaultBuilder().ConfigureWebHostDefaults(web => web.UseKestrel().UseUrls("http://127.0.0.1:0")
            .UseEnvironment("Production").UseContentRoot(environment.ContentRootPath)
            .UseSetting(WebHostDefaults.ApplicationKey, environment.ApplicationName)
            .ConfigureLogging(builder => builder.AddProvider(logs))
            .ConfigureServices(services =>
            {
                startup.ConfigureServices(services);
                services.AddHttpsRedirection(options => options.HttpsPort = 443);
                services.AddControllers().AddApplicationPart(typeof(GarageSaas.Startup).Assembly)
                    .AddApplicationPart(typeof(ProductionSecurityProbeController).Assembly);
            })
            .Configure(app =>
            {
                // Emulate the scheme supplied by a trusted TLS terminator in this test only.
                app.Use((context, next) => { if (context.Request.Headers["X-Test-Https"] == "1") context.Request.Scheme = "https"; return next(); });
                startup.Configure(app, environment);
            })).Build();
        await host.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            { BaseAddress = new Uri(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            using var redirect = await client.GetAsync("/ProductionSecurityProbe/Token");
            Assert.AreEqual(HttpStatusCode.TemporaryRedirect, redirect.StatusCode);
            Assert.AreEqual("https", redirect.Headers.Location!.Scheme);
            client.DefaultRequestHeaders.Add("X-Test-Https", "1");
            using var health = await client.GetAsync("/health");
            Assert.AreEqual(HttpStatusCode.OK, health.StatusCode);
            Assert.AreEqual("Healthy", await health.Content.ReadAsStringAsync());
            var logoName = "garage-1-" + Guid.NewGuid().ToString("N") + ".png";
            var logoBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestHelpers", "Fixtures", "GarageLogo.png"));
            File.WriteAllBytes(Path.Combine(logoDirectory, logoName), logoBytes);
            using var logo = await client.GetAsync("/uploads/garage-logos/" + logoName);
            Assert.AreEqual(HttpStatusCode.OK, logo.StatusCode);
            CollectionAssert.AreEqual(logoBytes, await logo.Content.ReadAsByteArrayAsync());
            using var page = await client.GetAsync("/ProductionSecurityProbe/Token");
            Assert.AreEqual(HttpStatusCode.OK, page.StatusCode);
            Assert.AreEqual("no-referrer", page.Headers.GetValues("Referrer-Policy").Single());
            var cookies = page.Headers.GetValues("Set-Cookie").ToArray();
            Assert.IsTrue(cookies.All(x => x.Contains("secure", StringComparison.OrdinalIgnoreCase) && x.Contains("httponly", StringComparison.OrdinalIgnoreCase)));
            var token = await page.Content.ReadAsStringAsync();
            using var rejected = await client.PostAsync("/ProductionSecurityProbe/Mutate", new StringContent(""));
            Assert.AreEqual(HttpStatusCode.BadRequest, rejected.StatusCode);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/ProductionSecurityProbe/Mutate");
            request.Headers.Add("Cookie", string.Join("; ", cookies.Select(x => x.Split(';')[0])));
            request.Headers.Add("RequestVerificationToken", token);
            using var accepted = await client.SendAsync(request);
            Assert.AreEqual(HttpStatusCode.OK, accepted.StatusCode);
            using var login = await client.GetAsync("/ProductionSecurityProbe/Cookie");
            Assert.IsTrue(login.Headers.GetValues("Set-Cookie").Any(x => x.Contains("secure", StringComparison.OrdinalIgnoreCase) && x.Contains("httponly", StringComparison.OrdinalIgnoreCase)));
            var oidc = host.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get("OpenIdConnect");
            Assert.IsFalse(oidc.SaveTokens); Assert.IsTrue(oidc.RequireHttpsMetadata);
            Assert.AreEqual("/signin-oidc", oidc.CallbackPath.Value);
            Assert.AreEqual("/signout-callback-oidc", oidc.SignedOutCallbackPath.Value);
            Assert.AreEqual("/", oidc.SignedOutRedirectUri);
            Assert.AreEqual(CookieSecurePolicy.Always, oidc.NonceCookie.SecurePolicy);
            using var error = await client.GetAsync("/ProductionSecurityProbe/Fail?token=synthetic-private");
            Assert.AreEqual(HttpStatusCode.InternalServerError, error.StatusCode);
            Assert.IsFalse((await error.Content.ReadAsStringAsync()).Contains("synthetic-private"));
            Assert.IsFalse(logs.Messages.Any(x => x.Contains("synthetic-private")), string.Join("\n", logs.Messages.Where(x => x.Contains("synthetic-private"))));
            Assert.IsTrue(logs.Messages.Any(x => x.Contains("InvalidOperationException")));
        }
        finally { await host.StopAsync(); Directory.Delete(logoDirectory, true); }
    }
    private sealed class Logs : ILoggerProvider
    {
        internal ConcurrentBag<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, Messages);
        public void Dispose() { }
    }
    private sealed class CaptureLogger(string category, ConcurrentBag<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => messages.Add(category + ": " + formatter(state, exception) + exception?.ToString());
    }
}

public class ProductionSecurityProbeController : Controller
{
    [HttpGet]
    public IActionResult Token([FromServices] IAntiforgery antiforgery) => Content(antiforgery.GetAndStoreTokens(HttpContext).RequestToken!);
    [HttpPost]
    public IActionResult Mutate() => Ok();
    [HttpGet]
    public async Task<IActionResult> Cookie()
    {
        TempData["Success"] = "synthetic feedback";
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "synthetic") }, "test")));
        return Ok();
    }
    [HttpGet]
    public IActionResult Fail() => throw new InvalidOperationException("synthetic-private customer details and token");
}

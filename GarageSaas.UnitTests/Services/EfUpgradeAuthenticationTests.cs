using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class EfUpgradeAuthenticationTests
{
    [TestMethod]
    public void ExistingIdentityRegistrationAndTokenValidationLoadWithSqlClientDependencies()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureAdB2C:Instance"] = "https://example.b2clogin.com/",
            ["AzureAdB2C:Domain"] = "example.onmicrosoft.com",
            ["AzureAdB2C:ClientId"] = "synthetic-client",
            ["AzureAdB2C:SignUpSignInPolicyId"] = "B2C_1_test",
            ["AzureAdB2C:CallbackPath"] = "/signin-oidc"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMicrosoftIdentityWebAppAuthentication(configuration, "AzureAdB2C");
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
        Assert.AreEqual("synthetic-client", options.ClientId);
        Assert.IsNotNull(options.SecurityTokenValidator);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("synthetic-test-signing-key-32-bytes-minimum"));
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Issuer = "test-issuer", Audience = "synthetic-client", Expires = DateTime.UtcNow.AddMinutes(5),
            Subject = new ClaimsIdentity(new[] { new Claim("sub", "test-user") }),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidIssuer = "test-issuer", ValidAudience = "synthetic-client", IssuerSigningKey = key
        }, out _);
        Assert.IsTrue(principal.Identity!.IsAuthenticated);
    }
}

using GarageSaas.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class ProductionConfigurationTests
{
    internal static Dictionary<string, string?> Settings() => new()
    {
        ["ConnectionStrings:GarageSaasDBConnection"] = "Server=sql.example.invalid;Database=synthetic;User ID=test;Password=synthetic-secret;Encrypt=True;TrustServerCertificate=False",
        ["AzureAdB2C:Instance"] = "https://example.b2clogin.com/",
        ["AzureAdB2C:Domain"] = "example.onmicrosoft.com",
        ["AzureAdB2C:ClientId"] = "11111111-1111-1111-1111-111111111111",
        ["AzureAdB2C:ClientSecret"] = "synthetic-secret",
        ["AzureAdB2C:SignUpSignInPolicyId"] = "B2C_1_test",
        ["AllowedHosts"] = "garage.example.invalid",
        ["Onboarding:PublicOrigin"] = "https://garage.example.invalid",
        ["EmailSettings:SmtpHost"] = "smtp.example.invalid",
        ["EmailSettings:SmtpPort"] = "587",
        ["EmailSettings:SenderEmail"] = "garage@example.invalid",
        ["EmailSettings:UseStartTls"] = "true"
    };
    internal static IConfiguration Config(Dictionary<string, string?> settings) => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    [DataTestMethod]
    [DataRow("ConnectionStrings:GarageSaasDBConnection")]
    [DataRow("AzureAdB2C:ClientSecret")]
    [DataRow("AzureAdB2C:ClientId")]
    [DataRow("AzureAdB2C:SignUpSignInPolicyId")]
    [DataRow("AllowedHosts")]
    [DataRow("Onboarding:PublicOrigin")]
    [DataRow("EmailSettings:SmtpHost")]
    [DataRow("EmailSettings:SenderEmail")]
    public void ProductionStartupRejectsMissingEssentialConfigurationWithoutSecretValues(string key)
    {
        var settings = Settings(); settings[key] = "";
        var exception = Assert.ThrowsException<InvalidOperationException>(() =>
            new GarageSaas.Startup(Config(settings), new Environment("Production")).ConfigureServices(new ServiceCollection()));
        Assert.IsFalse(exception.ToString().Contains("synthetic-secret"));
        StringAssert.Contains(exception.Message, key);
    }
    [DataTestMethod]
    [DataRow("ConnectionStrings:GarageSaasDBConnection", "Password=synthetic-secret;Unknown=synthetic-secret")]
    [DataRow("ConnectionStrings:GarageSaasDBConnection", "Server=sql;Database=test;Encrypt=False")]
    [DataRow("ConnectionStrings:GarageSaasDBConnection", "Server=sql;Database=test;TrustServerCertificate=True")]
    [DataRow("EmailSettings:UseStartTls", "false")]
    [DataRow("EmailSettings:SmtpPort", "synthetic-secret")]
    [DataRow("AllowedHosts", "*")]
    [DataRow("Onboarding:PublicOrigin", "https://example.invalid/path?token=synthetic-secret")]
    [DataRow("OpenAI:GarageRequestsPerMinute", "0")]
    [DataRow("OpenAI:RequestsPerMinute", "synthetic-secret")]
    [DataRow("OpenAI:ApiKey", "synthetic-secret\n")]
    [DataRow("AzureAdB2C:ClientSecret", "@Microsoft.KeyVault(SecretUri=https://synthetic-secret.vault.azure.net/secrets/client/)")]
    [DataRow("OpenAI:ApiKey", "@Microsoft.KeyVault(SecretUri=https://synthetic-secret.vault.azure.net/secrets/api/)")]
    public void UnsafeConfigurationFailsWithSanitizedErrors(string key, string value)
    {
        var settings = Settings(); settings[key] = value;
        var exception = Assert.ThrowsException<InvalidOperationException>(() => ProductionConfiguration.Validate(Config(settings)));
        Assert.IsFalse(exception.ToString().Contains("synthetic-secret"));
        Assert.IsNull(exception.InnerException);
    }
    [TestMethod]
    public void ValidProductionAllowsOptionalAiAndDevelopmentDoesNotRequireProductionSecrets()
    {
        ProductionConfiguration.Validate(Config(Settings()));
        new GarageSaas.Startup(Config(new()), new Environment("Development")).ConfigureServices(new ServiceCollection());
        Assert.ThrowsException<InvalidOperationException>(() => { using var db = new SignupContext(); _ = db.Model; });
    }
    internal sealed class Environment(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = typeof(GarageSaas.Startup).Assembly.GetName().Name!;
        public string ContentRootPath { get; set; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../GarageSaas"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}

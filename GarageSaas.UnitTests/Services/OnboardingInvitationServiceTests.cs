using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class OnboardingInvitationServiceTests
{
    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("http://garage.example.com")]
    [DataRow("//garage.example.com")]
    [DataRow("https://user:password@garage.example.com")]
    [DataRow("https://garage.example.com/path")]
    [DataRow("https://garage.example.com?redirect=evil")]
    [DataRow("https://garage.example.com#fragment")]
    public void CreateUrl_RejectsUntrustedOriginConfiguration(string? origin)
    {
        var service = CreateService(origin, Mock.Of<IEmailService>());
        Assert.ThrowsException<InvalidOperationException>(() => service.CreateUrl("secret"));
    }

    [DataTestMethod]
    [DataRow("https://garage.example.com")]
    [DataRow("https://garage.example.com/")]
    [DataRow("https://localhost:7001")]
    public void CreateUrl_UsesOnlyConfiguredHttpsOriginAndEscapesToken(string origin)
    {
        var url = CreateService(origin, Mock.Of<IEmailService>()).CreateUrl("a&next=https://evil.example");
        Assert.AreEqual(origin.TrimEnd('/') + "/GarageOwnerOnboarding/Accept?token=a%26next%3Dhttps%3A%2F%2Fevil.example", url);
    }

    [TestMethod]
    public async Task Send_EncodesContentAndReturnsMailServerFailureWithoutLoggingSecrets()
    {
        var email = new Mock<IEmailService>();
        string body = "";
        email.Setup(x => x.SendEmailAsync("owner@example.com", It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, html) => body = html)
            .ThrowsAsync(new IOException("SMTP failure containing secret-token"));
        var logger = new CapturingLogger();
        var service = CreateService("https://garage.example.com", email.Object, logger);
        var sent = await service.SendAsync(new GarageRegistrationRequest
        {
            Id = 7, ContactEmail = "owner@example.com", GarageBusinessName = "<script>garage</script>"
        }, "https://garage.example.com/accept?token=secret-token&other=1");

        Assert.IsFalse(sent);
        StringAssert.Contains(body, "&lt;script&gt;");
        StringAssert.Contains(body, "&amp;other=1");
        Assert.IsFalse(logger.Message.Contains("secret-token"));
        Assert.IsNull(logger.Exception);
        StringAssert.Contains(logger.Message, "7");
        email.VerifyAll();
    }

    internal static OnboardingInvitationService CreateService(string? origin, IEmailService email,
        ILogger<OnboardingInvitationService>? logger = null) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Onboarding:PublicOrigin"] = origin }).Build(), email,
        logger ?? NullLogger<OnboardingInvitationService>.Instance);

    private sealed class CapturingLogger : ILogger<OnboardingInvitationService>
    {
        public string Message = "";
        public Exception? Exception;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Message = formatter(state, exception);
            Exception = exception;
        }
    }
}

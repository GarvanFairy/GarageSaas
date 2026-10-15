using System.Globalization;
using System.Security.Claims;
using GarageSaas.Controllers;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.UnitTests.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Controllers;

// Opt-in SQL Server tests. Always create/drop our own randomly named database;
// never read application configuration or connect to an existing application database.
[TestClass]
[DoNotParallelize]
public class RegistrationConcurrencyTests
{
    private static string? _connection;
    private static string? _database;

    [ClassInitialize]
    public static async Task Initialize(TestContext _)
    {
        var server = Environment.GetEnvironmentVariable("GARAGESAAS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(server))
            Assert.Inconclusive("Set GARAGESAAS_TEST_SQLSERVER to an isolated SQL Server/LocalDB server to run locking tests.");
        _database = "GarageSaas_Test_" + Guid.NewGuid().ToString("N");
        _connection = new SqlConnectionStringBuilder
        {
            DataSource = server, InitialCatalog = _database, IntegratedSecurity = true,
            TrustServerCertificate = true, ConnectTimeout = 15
        }.ConnectionString;
        using var context = Context();
        await context.Database.EnsureCreatedAsync();
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        if (_connection == null) return;
        var builder = new SqlConnectionStringBuilder(_connection);
        if (builder.InitialCatalog != _database || !_database!.StartsWith("GarageSaas_Test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to delete a database not owned by this test fixture.");
        using var context = Context();
        await context.Database.EnsureDeletedAsync();
    }

    [TestMethod]
    public async Task RowLock_BlocksCompetingMutationAndLoadsFreshState()
    {
        var seed = await Seed("Approved");
        using var first = Context();
        using var second = Context();
        // Intentionally track stale state in the second scope to exercise refresh.
        await second.GarageRegistrationRequest.AsQueryable().SingleAsync(x => x.Id == seed.Id);
        var acquired = Signal();
        var release = Signal();
        var entered = Signal();
        var firstTask = Lock(first).ExecuteAsync(seed.Id, async request =>
        {
            acquired.SetResult(true);
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            request.Status = "Completed";
            await first.SaveChangesAsync();
            return true;
        });
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var secondTask = Lock(second).ExecuteAsync(seed.Id, request =>
        {
            entered.SetResult(true);
            return Task.FromResult(request.Status);
        });
        try
        {
            await Task.Delay(200);
            Assert.IsFalse(entered.Task.IsCompleted, "A competing mutation entered while the first request lock was held.");
        }
        finally { release.TrySetResult(true); }
        await firstTask;
        Assert.AreEqual("Completed", await secondTask);
    }

    [TestMethod]
    public async Task ConcurrentOwnerCompletion_CreatesExactlyOneGarageAndMembership()
    {
        var seed = await Seed("Approved");
        using var first = Context();
        using var second = Context();
        var results = await Task.WhenAll(Owner(first, seed.ExternalId).Complete(seed.Token),
            Owner(second, seed.ExternalId).Complete(seed.Token));
        Assert.AreEqual(1, results.OfType<RedirectToActionResult>().Count());
        Assert.AreEqual("InvalidInvitation", results.OfType<ViewResult>().Single().ViewName);
        using var check = Context();
        Assert.AreEqual(1, await check.GarageBusiness.AsQueryable().CountAsync(x => x.GarageBusinessName == seed.Name));
        Assert.AreEqual(1, await check.GarageBusinessUser.AsQueryable().CountAsync(x => x.UserId == seed.UserId));
        var saved = await check.GarageRegistrationRequest.AsQueryable().SingleAsync(x => x.Id == seed.Id);
        Assert.AreEqual("Completed", saved.Status);
        Assert.IsNotNull(saved.CompletedDate);
        Assert.IsTrue(saved.OnboardingRevoked);
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(-1)]
    public async Task ConcurrentResends_RotateOncePreserveAuditAndSendOnce(int expiryDays)
    {
        var seed = await Seed("Approved", expiryDays: expiryDays);
        using var first = Context();
        using var second = Context();
        var email = new Mock<IEmailService>();
        string sentBody = "";
        email.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, body) => sentBody = body)
            .Returns(Task.CompletedTask);
        await Task.WhenAll(Admin(first, seed.ExternalId, email.Object).ResendOnboardingInvitation(seed.Id, seed.Expiry),
            Admin(second, seed.ExternalId, email.Object).ResendOnboardingInvitation(seed.Id, seed.Expiry));
        using var check = Context();
        var saved = await check.GarageRegistrationRequest.AsQueryable().SingleAsync(x => x.Id == seed.Id);
        Assert.AreNotEqual(GarageInvitationTokenService.HashToken(seed.Token), saved.OnboardingTokenHash);
        Assert.IsTrue(saved.OnboardingTokenExpiresDate > DateTime.UtcNow.AddDays(6));
        Assert.AreEqual("original-reviewer@example.com", saved.ReviewedBy);
        Assert.AreEqual(seed.Reviewed, saved.ReviewedDate);
        Assert.AreEqual("Approved", saved.Status);
        StringAssert.Contains(sentBody, "https://trusted.example/GarageOwnerOnboarding/Accept?token=");
        Assert.IsFalse(sentBody.Contains("attacker.example"));
        var sentToken = System.Text.RegularExpressions.Regex.Match(sentBody, "token=([^\"&]+)").Groups[1].Value;
        Assert.AreEqual(saved.OnboardingTokenHash, GarageInvitationTokenService.HashToken(sentToken));
        email.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task ResendAfterCompletionCannotReviveInvitation()
    {
        var seed = await Seed("Approved");
        using var owner = Context();
        await Owner(owner, seed.ExternalId).Complete(seed.Token);
        using var admin = Context();
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        await Admin(admin, seed.ExternalId, email.Object).ResendOnboardingInvitation(seed.Id, seed.Expiry);
        using var check = Context();
        var saved = await check.GarageRegistrationRequest.AsQueryable().SingleAsync(x => x.Id == seed.Id);
        Assert.AreEqual("Completed", saved.Status);
        Assert.IsTrue(saved.OnboardingRevoked);
        email.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task OwnerCompletion_RechecksTokenAfterConcurrentRotation()
    {
        var seed = await Seed("Approved");
        using var ownerContext = Context();
        using var adminContext = Context();
        var resolved = Signal();
        var resume = Signal();
        var delayedLock = new DelayedLock(Lock(ownerContext), resolved, resume);
        var controller = Owner(ownerContext, seed.ExternalId, delayedLock);
        var completion = controller.Complete(seed.Token);
        await resolved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await Admin(adminContext, seed.ExternalId, Mock.Of<IEmailService>())
                .ResendOnboardingInvitation(seed.Id, seed.Expiry);
        }
        finally { resume.TrySetResult(true); }
        var result = (ViewResult)await completion;
        Assert.AreEqual("InvalidInvitation", result.ViewName);
        using var check = Context();
        Assert.AreEqual(0, await check.GarageBusiness.AsQueryable().CountAsync(x => x.GarageBusinessName == seed.Name));
    }

    [TestMethod]
    public async Task EmailFailure_HappensAfterCommitAndLeavesApprovedRequestRecoverable()
    {
        var seed = await Seed("Pending");
        using var context = Context();
        var email = new Mock<IEmailService>();
        email.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(async () =>
            {
                // A separate SQL connection must be able to lock/read the committed row during SMTP.
                using var check = Context();
                await Lock(check).ExecuteAsync(seed.Id, request =>
                {
                    Assert.AreEqual("Approved", request.Status);
                    Assert.IsFalse(string.IsNullOrEmpty(request.OnboardingTokenHash));
                    return Task.FromResult(true);
                });
                throw new IOException("Simulated SMTP timeout with secret content");
            });
        var controller = Admin(context, seed.ExternalId, email.Object);
        await controller.ApproveRegistration(seed.Id);
        StringAssert.Contains((string)controller.TempData["Error"]!, "delivery could not be confirmed");
        using var savedContext = Context();
        var saved = await savedContext.GarageRegistrationRequest.AsQueryable().SingleAsync(x => x.Id == seed.Id);
        Assert.AreEqual("Approved", saved.Status);
        var hash = saved.OnboardingTokenHash;
        var expiry = saved.OnboardingTokenExpiresDate!.Value.ToString("O", CultureInfo.InvariantCulture);
        var cooldown = Admin(savedContext, seed.ExternalId, Mock.Of<IEmailService>());
        await cooldown.ResendOnboardingInvitation(seed.Id, expiry);
        StringAssert.Contains((string)cooldown.TempData["Error"]!, "one minute");
        using var final = Context();
        Assert.AreEqual(hash, (await final.GarageRegistrationRequest.AsQueryable().SingleAsync(x => x.Id == seed.Id)).OnboardingTokenHash,
            "Cooldown must prevent immediate repeated token rotation after an SMTP failure.");
    }

    [DataTestMethod]
    [DataRow("Pending", false)]
    [DataRow("Rejected", false)]
    [DataRow("Completed", false)]
    [DataRow("Approved", true)]
    public async Task Resend_RejectsIneligibleRequests(string status, bool revoked)
    {
        var seed = await Seed(status, revoked);
        using var context = Context();
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        var controller = Admin(context, seed.ExternalId, email.Object);
        await controller.ResendOnboardingInvitation(seed.Id, seed.Expiry);
        Assert.IsNotNull(controller.TempData["Error"]);
        using var check = Context();
        Assert.AreEqual(GarageInvitationTokenService.HashToken(seed.Token),
            (await check.GarageRegistrationRequest.AsQueryable().SingleAsync(x => x.Id == seed.Id)).OnboardingTokenHash);
        email.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task InvalidOrigin_DoesNotApproveOrRotate()
    {
        var seed = await Seed("Pending");
        using var context = Context();
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        var controller = Admin(context, seed.ExternalId, email.Object, "http://untrusted.example");
        await controller.ApproveRegistration(seed.Id);
        using var check = Context();
        Assert.AreEqual("Pending", (await check.GarageRegistrationRequest.AsQueryable().SingleAsync(x => x.Id == seed.Id)).Status);
        email.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ConcurrentApprovalAndRejection_CommitOnlyOneDecision()
    {
        var seed = await Seed("Pending");
        using var first = Context();
        using var second = Context();
        var email = new Mock<IEmailService>();
        email.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        var approval = Admin(first, seed.ExternalId, email.Object);
        var rejection = Admin(second, seed.ExternalId, email.Object);
        await Task.WhenAll(approval.ApproveRegistration(seed.Id), rejection.RejectRegistration(seed.Id));
        Assert.AreEqual(1, new[] { approval, rejection }.Count(x => x.TempData.ContainsKey("Error")));
        using var check = Context();
        var saved = await check.GarageRegistrationRequest.AsQueryable().SingleAsync(x => x.Id == seed.Id);
        Assert.IsTrue(saved.Status == "Approved" || saved.Status == "Rejected");
        email.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            saved.Status == "Approved" ? Times.Once() : Times.Never());
    }

    [TestMethod]
    public async Task FailedMutation_RollsBackPartialSavesAndClearsTrackedState()
    {
        var seed = await Seed("Approved");
        using var context = Context();
        await Assert.ThrowsExceptionAsync<RegistrationRequestUnavailableException>(() =>
            Lock(context).ExecuteAsync<bool>(seed.Id, async request =>
            {
                request.Status = "Completed";
                await context.SaveChangesAsync();
                throw new DbUpdateException("Simulated failure with secret token material");
            }));
        using var check = Context();
        Assert.AreEqual("Approved", (await check.GarageRegistrationRequest.AsQueryable().SingleAsync(x => x.Id == seed.Id)).Status);
        Assert.AreEqual(0, context.ChangeTracker.Entries().Count());
    }

    [TestMethod]
    public void AdministratorActions_KeepPolicyAndAntiforgeryProtection()
    {
        Assert.AreEqual("PlatformAdministrator", ((AuthorizeAttribute)Attribute.GetCustomAttribute(
            typeof(PlatformAdminController), typeof(AuthorizeAttribute))!).Policy);
        foreach (var method in new[] { "ApproveRegistration", "RejectRegistration", "ResendOnboardingInvitation" })
        {
            var action = typeof(PlatformAdminController).GetMethod(method)!;
            Assert.IsTrue(Attribute.IsDefined(action, typeof(HttpPostAttribute)));
            Assert.IsTrue(Attribute.IsDefined(action, typeof(ValidateAntiForgeryTokenAttribute)));
        }
    }

    private static SignupContext Context() => new(new DbContextOptionsBuilder<SignupContext>()
        .UseSqlServer(_connection).Options);
    private static RegistrationRequestLock Lock(SignupContext context) => new(context, NullLogger<RegistrationRequestLock>.Instance);
    private static TaskCompletionSource<bool> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static T SetHttp<T>(T controller, string externalId) where T : Controller
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, externalId), new Claim("emails", externalId + "@example.com")
        }, "Test")) };
        http.Request.Host = new HostString("attacker.example");
        http.Request.Scheme = "http";
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());
        return controller;
    }

    private static PlatformAdminController Admin(SignupContext context, string externalId, IEmailService email,
        string origin = "https://trusted.example") => SetHttp(new PlatformAdminController(context, Lock(context),
            OnboardingInvitationServiceTests.CreateService(origin, email)), externalId);

    private static GarageOwnerOnboardingController Owner(SignupContext context, string externalId,
        IRegistrationRequestLock? requestLock = null) => SetHttp(new GarageOwnerOnboardingController(context,
            new InvitationIdentityVerificationService(), requestLock ?? Lock(context),
            NullLogger<GarageOwnerOnboardingController>.Instance), externalId);

    private static async Task<SeedData> Seed(string status, bool revoked = false, int expiryDays = 1)
    {
        using var context = Context();
        var external = Guid.NewGuid().ToString("N");
        var token = GarageInvitationTokenService.GenerateToken();
        var user = new Users { ExternalUserId = external, EmailAddress = external + "@example.com",
            FirstName = "Test", LastName = "Owner", Active = true, IsPlatformAdministrator = true,
            CreatedDate = DateTime.UtcNow };
        var request = new GarageRegistrationRequest
        {
            GarageBusinessName = "Test-" + external, AddressLine1 = "Main Street", TownOrCity = "Dublin",
            County = "Dublin", BusinessPhone = "12345", BusinessType = "Garage", ContactFirstName = "Test",
            ContactLastName = "Owner", ContactEmail = user.EmailAddress, ContactMobile = "12345",
            Status = status, CreatedDate = DateTime.UtcNow, ReviewedDate = DateTime.UtcNow.AddDays(-2),
            ReviewedBy = "original-reviewer@example.com", OnboardingTokenHash = GarageInvitationTokenService.HashToken(token),
            OnboardingTokenExpiresDate = DateTime.UtcNow.AddDays(expiryDays), OnboardingRevoked = revoked
        };
        context.Add(user);
        context.Add(request);
        await context.SaveChangesAsync();
        await context.Entry(request).ReloadAsync();
        return new SeedData(request.Id, user.Id, external, token, request.GarageBusinessName,
            request.OnboardingTokenExpiresDate!.Value.ToString("O", CultureInfo.InvariantCulture), request.ReviewedDate);
    }

    private record SeedData(int Id, int UserId, string ExternalId, string Token, string Name, string Expiry, DateTime? Reviewed);

    private sealed class DelayedLock(IRegistrationRequestLock inner, TaskCompletionSource<bool> resolved,
        TaskCompletionSource<bool> resume) : IRegistrationRequestLock
    {
        public async Task<T> ExecuteAsync<T>(int id, Func<GarageRegistrationRequest, Task<T>> action)
        {
            resolved.TrySetResult(true);
            await resume.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return await inner.ExecuteAsync(id, action);
        }
    }
}


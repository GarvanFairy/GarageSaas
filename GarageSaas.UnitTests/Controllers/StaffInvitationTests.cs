using System.Security.Claims;
using System.Text.Json;
using GarageSaas.Controllers;
using GarageSaas.Exceptions;
using GarageSaas.Models;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class StaffInvitationTests
{
    [DataTestMethod]
    [DataRow("active", "get")] [DataRow("inactive", "get")] [DataRow("blocked", "get")] [DataRow("missing", "get")]
    [DataRow("active", "signin")] [DataRow("inactive", "signin")] [DataRow("blocked", "signin")] [DataRow("missing", "signin")]
    [DataRow("active", "complete")] [DataRow("inactive", "complete")] [DataRow("blocked", "complete")] [DataRow("missing", "complete")]
    public void GarageAvailabilityAppliesWithoutExistingInviteeMembership(string garage, string action)
    {
        var options = StaffInvitationData.Options(); StaffInvitationData.Seed(options, garage);
        using var db = new SignupContext(options); var before = StaffInvitationData.Snapshot(db);
        var controller = StaffInvitationData.Invitation(db);
        var result = action == "get" ? controller.Accept(StaffInvitationData.Token) : action == "signin" ? controller.SignIn(StaffInvitationData.Token) : controller.Complete(StaffInvitationData.Token);
        if (garage != "active")
        { Assert.AreEqual("InvalidInvitation", ((ViewResult)result).ViewName); Assert.AreEqual(before, StaffInvitationData.Snapshot(db)); Assert.IsFalse(db.ChangeTracker.HasChanges()); }
        else if (action == "complete")
        { Assert.AreEqual("Home", ((RedirectToActionResult)result).ControllerName); Assert.IsTrue(db.GarageUserInvitation.Single().Accepted); Assert.IsTrue(db.GarageBusinessUser.Any(x => x.UserId == 2 && !x.IsOwner && x.IsActive)); }
        else Assert.IsInstanceOfType(result, action == "get" ? typeof(ViewResult) : typeof(ChallengeResult));
    }

    [DataTestMethod]
    [DataRow("expired", "get")] [DataRow("revoked", "get")] [DataRow("accepted", "get")] [DataRow("rotated", "get")]
    [DataRow("expired", "signin")] [DataRow("revoked", "signin")] [DataRow("accepted", "signin")] [DataRow("rotated", "signin")]
    [DataRow("expired", "complete")] [DataRow("revoked", "complete")] [DataRow("accepted", "complete")] [DataRow("rotated", "complete")]
    public void InvalidTokenStatesRemainRejected(string state, string action)
    {
        var options = StaffInvitationData.Options(); StaffInvitationData.Seed(options, invitation: state);
        using var db = new SignupContext(options); var before = StaffInvitationData.Snapshot(db); var c = StaffInvitationData.Invitation(db);
        var result = action == "get" ? c.Accept(StaffInvitationData.Token) : action == "signin" ? c.SignIn(StaffInvitationData.Token) : c.Complete(StaffInvitationData.Token);
        Assert.AreEqual("InvalidInvitation", ((ViewResult)result).ViewName); Assert.AreEqual(before, StaffInvitationData.Snapshot(db));
    }

    [DataTestMethod]
    [DataRow("blocked")] [DataRow("inactive")] [DataRow("inactive-membership")] [DataRow("wrong-email")]
    public void ExistingUserAndMembershipRestrictionsRemainUnchanged(string state)
    {
        var options = StaffInvitationData.Options(); StaffInvitationData.Seed(options, userState: state);
        using var db = new SignupContext(options); var before = StaffInvitationData.Snapshot(db);
        var c = StaffInvitationData.Invitation(db, state == "wrong-email" ? "wrong" : "invitee");
        Assert.IsInstanceOfType<ForbidResult>(c.Complete(StaffInvitationData.Token)); Assert.AreEqual(before, StaffInvitationData.Snapshot(db));
    }

    [TestMethod]
    public void NewB2CUserCanJoinAndSecondMembershipStillRequiresExplicitSelection()
    {
        foreach (var state in new[] { "new", "multiple", "existing-membership" })
        {
            var options = StaffInvitationData.Options(); StaffInvitationData.Seed(options, userState: state);
            using var db = new SignupContext(options); var c = StaffInvitationData.Invitation(db);
            Assert.IsInstanceOfType<RedirectToActionResult>(c.Complete(StaffInvitationData.Token));
            var user = db.Users.Single(x => x.ExternalUserId == "invitee");
            Assert.IsTrue(user.Active && !user.Blocked);
            if (state == "existing-membership") Assert.AreEqual("Administrator", db.GarageBusinessUser.Single(x => x.UserId == user.Id).Role);
            if (state == "multiple")
            {
                Assert.AreEqual(2, db.GarageBusinessUser.Count(x => x.UserId == user.Id));
                var accessor = new HttpContextAccessor { HttpContext = StaffInvitationData.Http("invitee") };
                Assert.IsTrue(Assert.ThrowsException<GarageAccessException>(() => new CurrentGarageUserService(db, accessor).GetCurrentUser()).SelectionRequired);
            }
        }
    }

    [DataTestMethod]
    [DataRow("inactive", false)] [DataRow("blocked", false)] [DataRow("missing", false)]
    [DataRow("inactive", true)] [DataRow("blocked", true)] [DataRow("missing", true)]
    public async Task TeamWritesRevalidateAfterCachedB21Success(string state, bool resend)
    {
        var options = StaffInvitationData.Options(); StaffInvitationData.Seed(options);
        using var db = new SignupContext(options);
        var accessor = new HttpContextAccessor { HttpContext = StaffInvitationData.Http("admin") };
        var current = new CurrentGarageUserService(db, accessor); current.GetCurrentUser();
        using (var change = new SignupContext(options))
        { var g = change.GarageBusiness.Single(); if (state == "missing") change.Remove(g); else { g.Active = state != "inactive"; g.Blocked = state == "blocked"; } change.SaveChanges(); }
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        var c = StaffInvitationData.Team(db, accessor, current, email.Object);
        var before = StaffInvitationData.Snapshot(db);
        var result = resend ? await c.ResendInvitation(1) : await c.Invite(StaffInvitationData.Input());
        Assert.IsInstanceOfType<ForbidResult>(result); Assert.AreEqual(before, StaffInvitationData.Snapshot(db)); Assert.IsFalse(db.ChangeTracker.HasChanges()); email.VerifyNoOtherCalls();
    }

    [DataTestMethod]
    [DataRow(null, false)] [DataRow("http://trusted.example", false)] [DataRow("https://trusted.example/path", false)]
    [DataRow("https://user:pass@trusted.example", false)] [DataRow("https://trusted.example?next=evil", false)] [DataRow("https://trusted.example#fragment", false)]
    [DataRow(null, true)] [DataRow("http://trusted.example", true)] [DataRow("https://trusted.example/path", true)]
    [DataRow("https://user:pass@trusted.example", true)] [DataRow("https://trusted.example?next=evil", true)] [DataRow("https://trusted.example#fragment", true)]
    public async Task InvalidOriginNeverCreatesOrRotatesInvitation(string? origin, bool resend)
    {
        var options = StaffInvitationData.Options(); StaffInvitationData.Seed(options);
        using var db = new SignupContext(options); var before = StaffInvitationData.Snapshot(db);
        var accessor = new HttpContextAccessor { HttpContext = StaffInvitationData.Http("admin") };
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        var c = StaffInvitationData.Team(db, accessor, new CurrentGarageUserService(db, accessor), email.Object, origin);
        var result = resend ? await c.ResendInvitation(1) : await c.Invite(StaffInvitationData.Input());
        Assert.IsInstanceOfType(result, resend ? typeof(RedirectToActionResult) : typeof(ViewResult));
        Assert.AreEqual(before, StaffInvitationData.Snapshot(db)); Assert.IsFalse(db.ChangeTracker.HasChanges()); email.VerifyNoOtherCalls();
    }

    [DataTestMethod]
    [DataRow("administrator")] [DataRow("Administrator")] [DataRow("Owner")] [DataRow("Staff")]
    public async Task IssuerRoleUsesExistingAuthorizationSemantics(string role)
    {
        var options = StaffInvitationData.Options(); StaffInvitationData.Seed(options, issuerRole: role);
        using var db = new SignupContext(options);
        var accessor = new HttpContextAccessor { HttpContext = StaffInvitationData.Http("admin") };
        var sent = new List<string>(); var email = StaffInvitationData.Email(sent);
        var c = StaffInvitationData.Team(db, accessor, new CurrentGarageUserService(db, accessor), email.Object);
        var allowed = role != "Staff";
        Assert.AreEqual(allowed, (await c.Invite()) is ViewResult);
        var result = await c.Invite(StaffInvitationData.Input());
        Assert.AreEqual(allowed, result is RedirectToActionResult);
        if (allowed) { Assert.AreEqual(1, sent.Count); StringAssert.StartsWith(sent[0], "https://trusted.example/GarageInvitation/Accept?token="); Assert.IsFalse(sent[0].Contains("attacker")); }
        else Assert.IsInstanceOfType<ForbidResult>(result);
    }
}

internal static class StaffInvitationData
{
    internal const string Token = "staff-test-token";
    private static readonly InMemoryDatabaseRoot Root = new();
    internal static DbContextOptions<SignupContext> Options() => new DbContextOptionsBuilder<SignupContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString(), Root)
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options;
    internal static void Seed(DbContextOptions<SignupContext> options, string garage = "active", string invitation = "pending", string userState = "active", string issuerRole = "Administrator")
    {
        using var db = new SignupContext(options);
        if (garage != "missing") db.Add(new GarageBusiness { Id = 1, GarageBusinessName = "Invited garage", Active = garage != "inactive", Blocked = garage == "blocked" });
        db.Add(new Users { LastName = "User", FirstName = "Test",  Id = 1, ExternalUserId = "admin", EmailAddress = "admin@test.invalid", Active = true });
        db.Add(new GarageBusinessUser { Id = 1, GarageBusinessId = 1, UserId = 1, Role = issuerRole, IsOwner = issuerRole == "Owner", IsActive = true });
        if (userState != "new") db.Add(new Users { LastName = "User", FirstName = "Test",  Id = 2, ExternalUserId = "invitee", EmailAddress = "invitee@test.invalid", Active = userState != "inactive", Blocked = userState == "blocked" });
        if (userState is "inactive-membership" or "existing-membership") db.Add(new GarageBusinessUser
        { Id = 2, GarageBusinessId = 1, UserId = 2, Role = "Administrator", IsActive = userState == "existing-membership" });
        if (userState == "multiple")
        { db.Add(new GarageBusiness { Id = 2, GarageBusinessName = "Other garage", Active = true }); db.Add(new GarageBusinessUser { Id = 2, GarageBusinessId = 2, UserId = 2, Role = "Staff", IsActive = true }); }
        db.Add(new GarageUserInvitation { Id = 1, GarageBusinessId = 1, EmailAddress = "invitee@test.invalid", FirstName = "Invited", LastName = "User", Role = "Staff",
            InvitationTokenHash = GarageInvitationTokenService.HashToken(invitation == "rotated" ? "new-token" : Token),
            InvitedByUserId = 1, ExpiresDate = DateTime.UtcNow.AddDays(invitation == "expired" ? -1 : 1), Accepted = invitation == "accepted", Revoked = invitation == "revoked" });
        db.SaveChanges();
    }
    internal static DefaultHttpContext Http(string? identity = "invitee")
    {
        var http = new DefaultHttpContext(); http.Request.Host = new HostString("attacker.example"); http.Request.Scheme = "http";
        if (identity != null) http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        { new Claim(ClaimTypes.NameIdentifier, identity), new Claim("emails", identity == "wrong" ? "wrong@test.invalid" : identity + "@test.invalid") }, "Test"));
        return http;
    }
    internal static GarageInvitationController Invitation(SignupContext db, string identity = "invitee")
    {
        var c = new GarageInvitationController(db, new InvitationIdentityVerificationService()) { ControllerContext = new ControllerContext { HttpContext = Http(identity) } };
        var url = new Mock<IUrlHelper>(); url.Setup(x => x.Action(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlActionContext>())).Returns("/GarageInvitation/Accept"); c.Url = url.Object; return c;
    }
    internal static InviteGarageUserViewModel Input() => new() { FirstName = "Another", LastName = "Employee", EmailAddress = "new@test.invalid", Role = "Staff" };
    internal static Mock<IEmailService> Email(List<string> urls)
    {
        var email = new Mock<IEmailService>(); email.Setup(x => x.SendGarageInvitationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string,string,string,string>((_,_,_,url) => urls.Add(url)).Returns(Task.CompletedTask); return email;
    }
    internal static GarageTeamController Team(SignupContext db, HttpContextAccessor accessor, ICurrentGarageUserService current, IEmailService email, string? origin = "https://trusted.example")
    {
        var c = new GarageTeamController(db, current, NullLogger<GarageTeamController>.Instance, email,
            new GarageAuthorizationService(current, accessor), new OnboardingInvitationService(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string,string?> { ["Onboarding:PublicOrigin"] = origin }).Build(), email, NullLogger<OnboardingInvitationService>.Instance));
        c.ControllerContext = new ControllerContext { HttpContext = accessor.HttpContext! };
        c.TempData = new TempDataDictionary(accessor.HttpContext!, Mock.Of<ITempDataProvider>()); return c;
    }
    internal static string Snapshot(SignupContext db) => JsonSerializer.Serialize(new
    {
        Users = db.Users.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id,x.ExternalUserId,x.EmailAddress,x.Active,x.Blocked,x.GarageBusinessId,x.Admin_Owner,x.UpdatedDate,x.UpdatedBy }).ToList(),
        Memberships = db.GarageBusinessUser.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id,x.GarageBusinessId,x.UserId,x.Role,x.IsActive,x.IsOwner,x.UpdatedBy,x.UpdatedDate }).ToList(),
        Invitations = db.GarageUserInvitation.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id,x.GarageBusinessId,x.Accepted,x.AcceptedDate,x.Revoked,x.RevokedDate,x.InvitationTokenHash,x.ExpiresDate }).ToList()
    });
}

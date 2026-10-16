using System.Security.Claims;
using GarageSaas.Exceptions;
using GarageSaas.Services;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class CurrentGarageUserServiceTests
{
    internal static HttpContextAccessor Accessor(string? externalId = "identity", bool authenticated = true) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(externalId == null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.NameIdentifier, externalId) }, authenticated ? "test" : null))
        }
    };

    internal static void Seed(SignupContext db, bool owner = true, string role = "Owner")
    {
        db.Users.Add(new Users { Id = 1, ExternalUserId = "identity", FirstName = "Test", LastName = "User", Active = true, GarageBusinessId = 999, Admin_Owner = false });
        db.GarageBusiness.Add(new GarageBusiness { Id = 1, GarageBusinessName = "Garage", Active = true });
        db.GarageBusinessUser.Add(new GarageBusinessUser { Id = 1, UserId = 1, GarageBusinessId = 1, IsActive = true, IsOwner = owner, Role = role });
        db.SaveChanges();
    }

    [DataTestMethod]
    [DataRow("GET")]
    [DataRow("POST")]
    public void SingleEligibleMembershipSucceedsRegardlessOfLegacyFields(string method)
    {
        using var db = DbContextFactory.Create(); Seed(db);
        var accessor = Accessor(); accessor.HttpContext!.Request.Method = method;
        var user = new CurrentGarageUserService(db, accessor).GetCurrentUser();
        Assert.AreEqual(1, user.GarageBusinessId);
        Assert.IsTrue(user.IsOwner);
        Assert.AreEqual("Owner", user.Role);
    }

    [DataTestMethod]
    [DataRow("inactive-user")]
    [DataRow("blocked-user")]
    [DataRow("inactive-membership")]
    [DataRow("inactive-garage")]
    [DataRow("blocked-garage")]
    [DataRow("missing-user")]
    [DataRow("missing-membership")]
    [DataRow("missing-garage")]
    public void IneligibleOrMissingRelationshipsAreDenied(string reason)
    {
        using var db = DbContextFactory.Create(); Seed(db);
        var user = db.Users.Single(); var garage = db.GarageBusiness.Single(); var membership = db.GarageBusinessUser.Single();
        switch (reason)
        {
            case "inactive-user": user.Active = false; break;
            case "blocked-user": user.Blocked = true; break;
            case "inactive-membership": membership.IsActive = false; break;
            case "inactive-garage": garage.Active = false; break;
            case "blocked-garage": garage.Blocked = true; break;
            case "missing-user": user.ExternalUserId = "other-identity"; break;
            case "missing-membership": db.GarageBusinessUser.Remove(membership); break;
            case "missing-garage": membership.GarageBusinessId = 999; break;
        }
        db.SaveChanges();
        Assert.ThrowsException<GarageAccessException>(() => new CurrentGarageUserService(db, Accessor()).GetCurrentUser());
    }

    [DataTestMethod]
    [DataRow(true, true)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(false, false)]
    public void MultipleRecordsNeverFallBackEvenWhenOneIsEligible(bool secondActive, bool firstGarageAvailable)
    {
        using var db = DbContextFactory.Create(); Seed(db);
        db.GarageBusiness.Single().Active = firstGarageAvailable;
        db.GarageBusiness.Add(new GarageBusiness { Id = 2, GarageBusinessName = "Other", Active = true });
        db.GarageBusinessUser.Add(new GarageBusinessUser { UserId = 1, GarageBusinessId = 2, IsActive = secondActive, Role = "Staff" });
        db.SaveChanges();
        var exception = Assert.ThrowsException<GarageAccessException>(() => new CurrentGarageUserService(db, Accessor()).GetCurrentUser());
        Assert.IsTrue(exception.SelectionRequired);
    }

    [TestMethod]
    public void CachedSuccessIsStableAcrossInstancesAndCannotBeMutatedByConsumer()
    {
        using var db = DbContextFactory.Create(); Seed(db);
        var accessor = Accessor();
        var service = new CurrentGarageUserService(db, accessor);
        var first = service.GetCurrentUser(); first.GarageBusinessId = 999; first.IsOwner = false;
        db.GarageBusiness.Single().Active = false; db.SaveChanges();
        Assert.AreEqual(1, service.GetCurrentUser().GarageBusinessId);
        Assert.IsTrue(new CurrentGarageUserService(db, accessor).GetCurrentUser().IsOwner);
        accessor.HttpContext = Accessor().HttpContext;
        Assert.ThrowsException<GarageAccessException>(() => service.GetCurrentUser());
    }

    [TestMethod]
    public void CachedDenialCannotRecoverUntilNextRequest()
    {
        using var db = DbContextFactory.Create(); Seed(db);
        db.GarageBusiness.Single().Active = false; db.SaveChanges();
        var accessor = Accessor(); var service = new CurrentGarageUserService(db, accessor);
        Assert.ThrowsException<GarageAccessException>(() => service.GetCurrentUser());
        db.GarageBusiness.Single().Active = true; db.SaveChanges();
        Assert.ThrowsException<GarageAccessException>(() => new CurrentGarageUserService(db, accessor).GetCurrentUser());
        accessor.HttpContext = Accessor().HttpContext;
        Assert.AreEqual(1, service.GetCurrentUser().GarageBusinessId);
    }

    [TestMethod]
    public void AuthenticationAndNameIdentifierRemainRequired()
    {
        using var db = DbContextFactory.Create(); Seed(db);
        Assert.ThrowsException<UnauthorizedAccessException>(() => new CurrentGarageUserService(db, Accessor(authenticated: false)).GetCurrentUser());
        Assert.ThrowsException<GarageAccessException>(() => new CurrentGarageUserService(db, Accessor(null)).GetCurrentUser());
    }

    [DataTestMethod]
    [DataRow(true, "Staff", true, true)]
    [DataRow(false, "Administrator", false, true)]
    [DataRow(false, "administrator", false, true)]
    [DataRow(false, "Staff", false, false)]
    [DataRow(false, "Owner", false, false)]
    public async Task RoleSemanticsRemainMembershipBased(bool owner, string role, bool manageGarage, bool manageTeam)
    {
        using var db = DbContextFactory.Create(); Seed(db, owner, role);
        var accessor = Accessor(); var current = new CurrentGarageUserService(db, accessor);
        var authorization = new GarageAuthorizationService(current, accessor);
        Assert.AreEqual(owner, await authorization.IsOwnerAsync());
        Assert.AreEqual(manageGarage, await authorization.CanManageGarageAsync());
        Assert.AreEqual(manageTeam, await authorization.CanManageTeamAsync());
    }
}

using System.Security.Claims;
using GarageSaas.Authorization;
using GarageSaas.Controllers;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.UnitTests.TestHelpers;
using GarageSaas.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class PlatformGarageDirectoryTests
{
    [TestMethod]
    public void DirectoryActions_InheritPlatformPolicyAndAreReadOnly()
    {
        Assert.IsTrue(typeof(PlatformAdminController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Any(x => x.Policy == "PlatformAdministrator"));
        foreach (var name in new[] { "Garages", "GarageDetails" })
        {
            var method = typeof(PlatformAdminController).GetMethod(name)!;
            Assert.IsTrue(method.IsDefined(typeof(HttpGetAttribute), true));
            Assert.IsFalse(method.IsDefined(typeof(AllowAnonymousAttribute), true));
        }
    }

    [DataTestMethod]
    [DataRow(true, true, false, true, true)]
    [DataRow(true, true, false, false, false)]
    [DataRow(true, false, false, true, false)]
    [DataRow(true, true, true, true, false)]
    [DataRow(false, true, false, true, false)]
    public async Task Policy_RequiresAuthenticatedActiveUnblockedPlatformAdmin(bool authenticated, bool active, bool blocked, bool admin, bool allowed)
    {
        using var db = DbContextFactory.Create();
        db.Users.Add(new Users { ExternalUserId = "identity", FirstName = "A", LastName = "B", Active = active, Blocked = blocked, IsPlatformAdministrator = admin });
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "identity") }, authenticated ? "test" : null));
        var requirement = new PlatformAdministratorRequirement();
        var authorization = new AuthorizationHandlerContext(new[] { requirement }, principal, null);
        await new PlatformAdministratorHandler(db).HandleAsync(authorization);
        Assert.AreEqual(allowed, authorization.HasSucceeded);
        // No garage membership is required for platform administration.
        Assert.AreEqual(0, db.GarageBusinessUser.Count());
    }

    [TestMethod]
    public async Task Search_IsCaseInsensitiveTrimmedAndIncludesAllStatuses()
    {
        using var db = DbContextFactory.Create();
        db.GarageBusiness.AddRange(Garage(1, "Alpha", true), Garage(2, "ALPHA inactive", false), Garage(3, "alpha blocked", true, true), Garage(4, "Other", true));
        await db.SaveChangesAsync();
        db.ChangeTracker.Entries().ToList().ForEach(x => x.State = EntityState.Detached);
        var model = (PlatformGarageListViewModel)((ViewResult)await Controller(db).Garages(" aLpHa ")).Model!;
        Assert.AreEqual("aLpHa", model.Search);
        Assert.AreEqual(3, model.TotalCount);
        CollectionAssert.AreEqual(new[] { 1, 3, 2 }, model.Garages.Select(x => x.Id).ToArray());
        Assert.IsTrue(model.Garages.Any(x => !x.Active));
        Assert.IsTrue(model.Garages.Any(x => x.Blocked));
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
    }

    [TestMethod]
    public async Task Pagination_IsBoundedStableAndAppliedAfterSearch()
    {
        using var db = DbContextFactory.Create();
        for (var id = 45; id >= 1; id--) db.GarageBusiness.Add(Garage(id, "Same", true));
        db.GarageBusiness.Add(Garage(46, "Other", true));
        await db.SaveChangesAsync();
        var controller = Controller(db);
        var model = (PlatformGarageListViewModel)((ViewResult)await controller.Garages("same", 2)).Model!;
        Assert.AreEqual(45, model.TotalCount);
        Assert.AreEqual(3, model.TotalPages);
        CollectionAssert.AreEqual(Enumerable.Range(21, 20).ToArray(), model.Garages.Select(x => x.Id).ToArray());
        var last = (PlatformGarageListViewModel)((ViewResult)await controller.Garages("same", int.MaxValue)).Model!;
        Assert.AreEqual(3, last.Page);
        Assert.AreEqual(5, last.Garages.Count);
        var first = (PlatformGarageListViewModel)((ViewResult)await controller.Garages(null!, -1)).Model!;
        Assert.AreEqual(1, first.Page);
        var empty = (PlatformGarageListViewModel)((ViewResult)await controller.Garages("missing", 99)).Model!;
        Assert.AreEqual(1, empty.Page);
        Assert.AreEqual(0, empty.Garages.Count);
    }

    [TestMethod]
    public async Task Details_ProjectsOnlyRequestedGarageMembershipsAndUsesIsOwner()
    {
        using var db = DbContextFactory.Create();
        var garage = Garage(1, "Inactive blocked", false, true);
        garage.GarageAddressLine1 = "Main Street";
        garage.GarageEmailAddress = "garage@example.com";
        garage.CreatedBy = "creator";
        garage.UpdatedBy = "reviewer";
        garage.UpdatedDate = DateTime.UtcNow;
        db.GarageBusiness.AddRange(garage, Garage(2, "Other", true));
        for (var id = 1; id <= 3; id++)
            db.Users.Add(new Users { Id = id, FirstName = "Member", LastName = id.ToString(), Active = id != 2, Blocked = id == 2, ExternalUserId = "secret-" + id });
        db.GarageBusinessUser.AddRange(
            new GarageBusinessUser { Id = 1, GarageBusinessId = 1, UserId = 1, Role = "Staff", IsOwner = true, IsActive = true },
            new GarageBusinessUser { Id = 2, GarageBusinessId = 1, UserId = 2, Role = "Owner", IsOwner = true, IsActive = false },
            new GarageBusinessUser { Id = 3, GarageBusinessId = 2, UserId = 3, Role = "Owner", IsOwner = true, IsActive = true });
        await db.SaveChangesAsync();
        db.ChangeTracker.Entries().ToList().ForEach(x => x.State = EntityState.Detached);
        var model = (PlatformGarageDetailsViewModel)((ViewResult)await Controller(db).GarageDetails(1)).Model!;
        Assert.AreEqual(2, model.OwnerCount);
        Assert.AreEqual(2, model.Memberships.Count);
        Assert.AreEqual("Staff", model.Memberships[0].Role);
        Assert.IsTrue(model.Memberships[0].IsOwner);
        Assert.IsFalse(model.Memberships[1].IsActive);
        Assert.IsFalse(model.Memberships[1].UserActive);
        Assert.IsTrue(model.Memberships[1].UserBlocked);
        Assert.IsFalse(model.Active);
        Assert.IsTrue(model.Blocked);
        Assert.AreEqual("Main Street", model.AddressLine1);
        Assert.AreEqual("garage@example.com", model.Email);
        Assert.AreEqual("reviewer", model.UpdatedBy);
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
    }

    [TestMethod]
    public async Task Details_HandlesMissingGarageAndZeroOwners()
    {
        using var db = DbContextFactory.Create();
        db.GarageBusiness.Add(Garage(1, "Empty", true));
        await db.SaveChangesAsync();
        var controller = Controller(db);
        Assert.IsInstanceOfType(await controller.GarageDetails(0), typeof(NotFoundResult));
        Assert.IsInstanceOfType(await controller.GarageDetails(999), typeof(NotFoundResult));
        var model = (PlatformGarageDetailsViewModel)((ViewResult)await controller.GarageDetails(1)).Model!;
        Assert.AreEqual(0, model.OwnerCount);
        Assert.AreEqual(0, model.Memberships.Count);
    }

    private static GarageBusiness Garage(int id, string name, bool active, bool blocked = false) => new()
    { Id = id, GarageBusinessName = name, Active = active, Blocked = blocked };

    private static PlatformAdminController Controller(SignupContext db) => new(db, Mock.Of<IRegistrationRequestLock>(),
        new OnboardingInvitationService(new ConfigurationBuilder().Build(), Mock.Of<IEmailService>(), NullLogger<OnboardingInvitationService>.Instance));
}

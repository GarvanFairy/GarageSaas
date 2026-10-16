using GarageSaas.Services;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class GarageBusinessServiceTests
{
    [TestMethod]
    public void MissingUserIsDenied()
    {
        using var db = DbContextFactory.Create();
        var service = new GarageBusinessService(db, new CurrentGarageUserService(db, CurrentGarageUserServiceTests.Accessor()));
        Assert.IsFalse(service.GetGarageBusinessDetail(1, 999).Success);
    }

    [TestMethod]
    public void MembershipAllowsProfileDespiteDifferentLegacyGarage()
    {
        using var db = DbContextFactory.Create(); CurrentGarageUserServiceTests.Seed(db);
        var service = new GarageBusinessService(db, new CurrentGarageUserService(db, CurrentGarageUserServiceTests.Accessor()));
        Assert.IsTrue(service.GetGarageBusinessDetail(1, 1).Success);
        Assert.IsTrue(service.GetGarageBusinessForEdit(1, 1).Success);
        Assert.IsFalse(service.GetGarageBusinessDetail(999, 1).Success);
        Assert.IsFalse(service.GetGarageBusinessDetail(1, 999).Success);
    }

    [TestMethod]
    public void UpdateUsesTrustedGarageAndDoesNotChangeStatusOrLegacyFields()
    {
        using var db = DbContextFactory.Create(); CurrentGarageUserServiceTests.Seed(db);
        var service = new GarageBusinessService(db, new CurrentGarageUserService(db, CurrentGarageUserServiceTests.Accessor()));
        var result = service.UpdateGarageBusiness(new GarageBusiness { Id = 999, GarageBusinessName = "New", GarageAddressLine1 = "Address", GarageEmailAddress = "email@example.com", Active = false, Blocked = true }, 1, 1, "owner");
        Assert.IsTrue(result.Success);
        Assert.AreEqual("New", db.GarageBusiness.Single().GarageBusinessName);
        Assert.IsTrue(db.GarageBusiness.Single().Active);
        Assert.IsFalse(db.GarageBusiness.Single().Blocked);
        Assert.AreEqual("owner", db.GarageBusiness.Single().UpdatedBy);
        Assert.AreEqual(999, db.Users.Single().GarageBusinessId);
    }

    [DataTestMethod]
    [DataRow("Staff")]
    [DataRow("Administrator")]
    public void NonOwnersMayReadButCannotEditEvenIfLegacyOwner(string role)
    {
        using var db = DbContextFactory.Create(); CurrentGarageUserServiceTests.Seed(db, false, role);
        db.Users.Single().Admin_Owner = true; db.Users.Single().GarageBusinessId = 1; db.SaveChanges();
        var service = new GarageBusinessService(db, new CurrentGarageUserService(db, CurrentGarageUserServiceTests.Accessor()));
        Assert.IsTrue(service.GetGarageBusinessDetail(1, 1).Success);
        Assert.IsFalse(service.GetGarageBusinessForEdit(1, 1).Success);
        Assert.IsFalse(service.UpdateGarageBusiness(new GarageBusiness { GarageBusinessName = "forged" }, 1, 1, "forged").Success);
        Assert.AreEqual("Garage", db.GarageBusiness.Single().GarageBusinessName);
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, true)]
    public void UnavailableGarageIsDeniedForEveryProfileOperation(bool active, bool blocked)
    {
        using var db = DbContextFactory.Create(); CurrentGarageUserServiceTests.Seed(db);
        db.GarageBusiness.Single().Active = active; db.GarageBusiness.Single().Blocked = blocked; db.SaveChanges();
        var service = new GarageBusinessService(db, new CurrentGarageUserService(db, CurrentGarageUserServiceTests.Accessor()));
        Assert.IsFalse(service.GetGarageBusinessDetail(1, 1).Success);
        Assert.IsFalse(service.GetGarageBusinessForEdit(1, 1).Success);
        Assert.IsFalse(service.UpdateGarageBusiness(new GarageBusiness { GarageBusinessName = "forged" }, 1, 1, "forged").Success);
    }
}

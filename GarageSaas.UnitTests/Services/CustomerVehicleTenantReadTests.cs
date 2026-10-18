using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class CustomerVehicleTenantReadTests
{
    [DataTestMethod]
    [DataRow("foreign-roots")]
    [DataRow("foreign-vehicle")]
    [DataRow("foreign-customer")]
    [DataRow("foreign-label")]
    [DataRow("duplicate")]
    [DataRow("dangling-vehicle")]
    [DataRow("dangling-customer")]
    [DataRow("valid-owner")]
    [DataRow("valid-after-foreign-link")]
    [DataRow("ambiguous-owner")]
    [DataRow("garage-owned")]
    [DataRow("missing-lookups")]
    [DataRow("invalid-garage")]
    public async Task ReadsValidateAllOwnershipEndpointsWithoutWrites(string scenario)
    {
        using var db = DbContextFactory.Create();
        await CustomerVehicleReadCases.Check(db, scenario);
    }
}

// Same assertions are used by the optional real-SQL fixture, with generated IDs
// and all required parent records. No application connection/configuration used.
internal static class CustomerVehicleReadCases
{
    internal static async Task Check(SignupContext db, string scenario)
    {
        var now = DateTime.UtcNow;
        var a = new GarageBusiness { GarageBusinessName = "A", CreatedDate = now };
        var b = new GarageBusiness { GarageBusinessName = "B", CreatedDate = now };
        db.AddRange(a, b); db.SaveChanges();
        var ca = new GarageBusinessCustomer { GarageBusinessId = a.Id, GarageCustomerForename = "Local", CreatedDate = now };
        var cb = new GarageBusinessCustomer { GarageBusinessId = b.Id, GarageCustomerForename = "Foreign secret", CreatedDate = now };
        var second = new GarageBusinessCustomer { GarageBusinessId = a.Id, GarageCustomerForename = "Other local", CreatedDate = now };
        var make = new VehicleMake { Make = "Ford", Active = true };
        db.AddRange(ca, cb, second, make); db.SaveChanges();
        var model = new VehicleModel { MakeId = make.Id, Model = "Focus", Active = true };
        db.Add(model); db.SaveChanges();
        var va = new CustomerVehicle { GarageBusinessId = a.Id, GarageOwned = false,
            VehicleMakeId = make.Id, VehicleModelId = model.Id, VehicleRegistration = "LOCAL", CreatedDate = now, VehicleMileageDate = now };
        var vb = new CustomerVehicle { GarageBusinessId = b.Id, GarageOwned = false,
            VehicleMakeId = make.Id, VehicleModelId = model.Id, VehicleRegistration = "FOREIGN SECRET", CreatedDate = now, VehicleMileageDate = now };
        db.AddRange(va, vb);
        db.GarageVehicleOwner.Add(new GarageVehicleOwner { GarageBusinessId = a.Id, GarageVehicleOwnerName = "Garage A", CreatedDate = now });
        db.SaveChanges();
        CustomerOwnedVehicles Link(int customer, int vehicle, int garage) => new()
        { GarageBusinessCustomerId = customer, VehicleId = vehicle, GarageBusinessId = garage, CreatedDate = now };
        // B has a legitimate relationship in every case, to catch root-scope leaks.
        db.CustomerOwnedVehicles.Add(Link(cb.Id, vb.Id, b.Id));
        if (scenario == "valid-after-foreign-link") db.CustomerOwnedVehicles.Add(Link(cb.Id, va.Id, b.Id));
        var valid = scenario is "foreign-roots" or "valid-owner" or "valid-after-foreign-link" or "duplicate" or "ambiguous-owner" or "missing-lookups" or "invalid-garage";
        if (valid) db.CustomerOwnedVehicles.Add(Link(ca.Id, va.Id, a.Id));
        switch (scenario)
        {
            case "foreign-vehicle": db.CustomerOwnedVehicles.Add(Link(ca.Id, vb.Id, a.Id)); break;
            case "foreign-customer": db.CustomerOwnedVehicles.Add(Link(cb.Id, va.Id, a.Id)); break;
            case "foreign-label": db.CustomerOwnedVehicles.Add(Link(ca.Id, va.Id, b.Id)); break;
            case "duplicate": db.CustomerOwnedVehicles.Add(Link(ca.Id, va.Id, a.Id)); break;
            case "dangling-vehicle": db.CustomerOwnedVehicles.Add(Link(ca.Id, int.MaxValue, a.Id)); break;
            case "dangling-customer": db.CustomerOwnedVehicles.Add(Link(int.MaxValue, va.Id, a.Id)); break;
            case "ambiguous-owner": db.CustomerOwnedVehicles.Add(Link(second.Id, va.Id, a.Id)); break;
            case "garage-owned":
                va.GarageOwned = true;
                // Stale malformed link must not turn the garage owner into a customer.
                db.CustomerOwnedVehicles.Add(Link(cb.Id, va.Id, b.Id)); break;
            case "missing-lookups": va.VehicleMakeId = null; va.VehicleModelId = null; break;
        }
        db.SaveChanges();
        var linkRows = db.CustomerOwnedVehicles.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.GarageBusinessId, x.GarageBusinessCustomerId, x.VehicleId }).ToList();
        foreach (var entry in db.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
        var lookup = new Mock<IVehicleLookupService>();
        lookup.Setup(x => x.GetVehicleMakesAsync()).ReturnsAsync(() => new List<SelectListItem>());
        lookup.Setup(x => x.GetVehicleModelsAsync()).ReturnsAsync(() => new List<SelectListItem>());
        lookup.Setup(x => x.GetVehicleModelsByMakeAsync(It.IsAny<int>())).ReturnsAsync(() => new List<SelectListItem>());
        lookup.Setup(x => x.GetFuelTypesAsync()).ReturnsAsync(() => new List<SelectListItem>());
        lookup.Setup(x => x.GetVehicleYearsAsync()).ReturnsAsync(() => new List<SelectListItem>());
        lookup.Setup(x => x.GetMileageAsync()).ReturnsAsync(() => new List<SelectListItem>());
        lookup.Setup(x => x.GetTransmissionTypesAsync()).ReturnsAsync(() => new List<SelectListItem>());
        var vehicles = new CustomerVehicleService(db, lookup.Object);
        var customers = new GarageCustomersService(db, lookup.Object);
        if (scenario == "invalid-garage")
        {
            Assert.IsFalse(vehicles.GetCustomerVehiclesForList(0).Success);
            Assert.IsFalse((await vehicles.GetCustomerVehicleForEditAsync(va.Id, 0)).Success);
            Assert.IsFalse((await vehicles.GetVehicleBriefsForCustomerAsync(ca.Id, 0)).Success);
            Assert.IsFalse(customers.GetGarageCustomersForList(0).Success);
            Assert.IsFalse(customers.GetGarageCustomerForEdit(ca.Id, 0).Success);
        }
        else
        {
            Assert.IsFalse((await vehicles.GetCustomerVehicleForEditAsync(vb.Id, a.Id)).Success);
            Assert.IsFalse(customers.GetGarageCustomerForEdit(cb.Id, a.Id).Success);
            Assert.IsFalse((await vehicles.GetCustomerVehicleForEditAsync(va.Id, b.Id)).Success);
            Assert.IsFalse(customers.GetGarageCustomerForEdit(ca.Id, b.Id).Success);
            Assert.AreEqual(vb.Id, vehicles.GetCustomerVehiclesForList(b.Id).Data.Single().VehicleId);
            var foreign = await vehicles.GetVehicleBriefsForCustomerAsync(cb.Id, a.Id);
            var missing = await vehicles.GetVehicleBriefsForCustomerAsync(int.MaxValue, a.Id);
            Assert.IsFalse(foreign.Success); Assert.AreEqual(missing.ErrorMessage, foreign.ErrorMessage);
            var vehicleList = vehicles.GetCustomerVehiclesForList(a.Id);
            Assert.IsTrue(vehicleList.Success);
            Assert.IsTrue(vehicleList.Data.All(x => x.VehicleId == va.Id && x.GarageCustomerId != cb.Id));
            Assert.AreEqual(valid ? scenario == "ambiguous-owner" ? 2 : 1 : 0, vehicleList.Data.Count);
            var customerList = customers.GetGarageCustomersForList(a.Id);
            Assert.IsTrue(customerList.Success);
            Assert.IsTrue(customerList.Data.All(x => x.GarageCustomerId != cb.Id && x.VehicleId != vb.Id));
            // Malformed links cannot make an otherwise valid local customer disappear.
            Assert.AreEqual(1, customerList.Data.Count(x => x.GarageCustomerId == ca.Id));
            var customerEdit = customers.GetGarageCustomerForEdit(ca.Id, a.Id);
            var briefs = await vehicles.GetVehicleBriefsForCustomerAsync(ca.Id, a.Id);
            Assert.IsTrue(customerEdit.Success); Assert.IsTrue(briefs.Success);
            Assert.AreEqual(valid ? 1 : 0, customerEdit.Data.Vehicles.Count);
            Assert.AreEqual(valid ? 1 : 0, briefs.Data.Count);
            Assert.IsTrue(briefs.Data.All(x => x.Id == va.Id));
            var edit = await vehicles.GetCustomerVehicleForEditAsync(va.Id, a.Id);
            Assert.IsTrue(edit.Success);
            Assert.IsTrue(edit.Data.GarageVehicleOwnerList.All(x => x.Value != cb.Id.ToString()));
            var selected = edit.Data.GarageVehicleOwnerList.Where(x => x.Selected).ToList();
            if (scenario == "garage-owned") Assert.AreEqual("0", selected.Single().Value);
            else if (valid && scenario != "ambiguous-owner") Assert.AreEqual(ca.Id.ToString(), selected.Single().Value);
            else Assert.AreEqual(0, selected.Count);
            if (scenario == "missing-lookups") Assert.IsNull(briefs.Data.Single().Make);
        }
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count(), "Reads must not attach tenant entities");
        Assert.IsFalse(db.ChangeTracker.HasChanges());
        CollectionAssert.AreEqual(linkRows, db.CustomerOwnedVehicles.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.GarageBusinessId, x.GarageBusinessCustomerId, x.VehicleId }).ToList(),
            "Malformed ownership history must not be repaired by a read");
    }
}

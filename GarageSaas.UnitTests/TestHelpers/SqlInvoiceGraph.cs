using SignupAPI.Models;

namespace GarageSaas.UnitTests.TestHelpers;

internal sealed record SqlInvoiceGraph(int GarageId, int CustomerId, int VehicleId, int WorkItemId)
{
    internal static SqlInvoiceGraph Seed(SignupContext db)
    {
        var now = DateTime.UtcNow;
        var garage = new GarageBusiness { GarageBusinessName = "Isolated SQL garage", Active = true, CreatedDate = now, CreatedBy = "test" };
        db.GarageBusiness.Add(garage); db.SaveChanges();
        var customer = new GarageBusinessCustomer { GarageBusinessId = garage.Id, GarageCustomerForename = "SQL",
            GarageCustomerSurname = "Customer", CreatedDate = now, CreatedBy = "test" };
        var vehicle = new CustomerVehicle { GarageBusinessId = garage.Id, VehicleRegistration = "SQL TEST",
            VehicleMileageDate = now, CreatedDate = now, CreatedBy = "test", Active = true, GarageOwned = false };
        db.AddRange(customer, vehicle); db.SaveChanges();
        db.CustomerOwnedVehicles.Add(new CustomerOwnedVehicles { GarageBusinessId = garage.Id,
            GarageBusinessCustomerId = customer.Id, VehicleId = vehicle.Id, CreatedDate = now, CreatedBy = "test", Active = true });
        var item = new WorkItem { GarageBusinessCustomerId = garage.Id, CustomerId = customer.Id,
            VehicleId = vehicle.Id, RepairInstructions = "Same-garage SQL work", CreatedDate = now, CreatedBy = "test" };
        db.WorkItem.Add(item); db.SaveChanges();
        return new(garage.Id, customer.Id, vehicle.Id, item.Id);
    }
}

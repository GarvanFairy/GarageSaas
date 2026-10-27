using System.Text.Json;
using GarageSaas.Models;
using GarageSaas.Services;
using GarageSaas.Services.Models;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class OwnershipIntegrityTests
{
    private static readonly InMemoryDatabaseRoot Root = new();
    [DataTestMethod]
    [DataRow("reassign")] [DataRow("to-garage")] [DataRow("from-garage")]
    [DataRow("duplicate")] [DataRow("two-owners")] [DataRow("foreign-label")]
    [DataRow("foreign-owner")] [DataRow("dangling-owner")] [DataRow("contradictory")]
    [DataRow("foreign-target")] [DataRow("foreign-selected")]
    [DataRow("delete")] [DataRow("delete-invoice")] [DataRow("delete-legacy-invoice")]
    [DataRow("delete-quote")] [DataRow("delete-item")]
    [DataRow("delete-duplicate")] [DataRow("delete-foreign-label")]
    [DataRow("delete-foreign-vehicle")] [DataRow("delete-dangling-vehicle")]
    [DataRow("delete-contradictory")] [DataRow("delete-foreign-target")]
    [DataRow("reassign-fail-before")] [DataRow("reassign-fail-after")]
    [DataRow("delete-fail-before")] [DataRow("delete-fail-after")]
    public void OwnershipAndDeletionRules(string scenario)
        => OwnershipCases.Check(new DbContextOptionsBuilder<SignupContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), Root)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options, scenario, false);
}

internal static class OwnershipCases
{
    internal static VehicleAndCustomers Input(int vehicle, string owner) => new()
    {
        Vehicle = new CustomerVehicle { GarageOwned = false,  Id = vehicle, VehicleRegistration = "LOCAL EDIT", VehicleMileageDate = DateTime.UtcNow },
        GarageVehicleOwnerListItem = new SelectListItem { Value = owner }
    };
    internal static string Snapshot(SignupContext db) => JsonSerializer.Serialize(new
    {
        Customers = db.GarageBusinessCustomer.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id,x.GarageBusinessId,x.GarageCustomerForename,x.UpdatedDate,x.UpdatedBy }).ToList(),
        Vehicles = db.CustomerVehicle.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id,x.GarageBusinessId,x.GarageOwned,x.VehicleRegistration,x.UpdatedDate,x.UpdatedBy }).ToList(),
        Links = db.CustomerOwnedVehicles.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id,x.GarageBusinessId,x.GarageBusinessCustomerId,x.VehicleId,x.UpdatedBy,x.UpdatedDate }).ToList(),
        Items = db.WorkItem.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id,x.CustomerId,x.VehicleId }).ToList(),
        Quotes = db.WorkQuote.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id,x.CustomerId,x.VehicleId }).ToList(),
        Invoices = db.VehicleInvoice.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id,x.CustomerId,x.GarageBusinessCustomerId,x.VehicleId,x.Paid }).ToList()
    });
    internal static void Check(DbContextOptions<SignupContext> options, string scenario, bool relational)
    {
        SqlInvoiceGraph a,b; int replacement;
        using (var seed = new SignupContext(options))
        {
            a = SqlInvoiceGraph.Seed(seed); b = SqlInvoiceGraph.Seed(seed);
            seed.WorkItem.RemoveRange(seed.WorkItem); seed.SaveChanges();
            var customer = new GarageBusinessCustomer { GarageBusinessId = a.GarageId, GarageCustomerForename = "Replacement", CreatedDate = DateTime.UtcNow };
            seed.Add(customer); seed.SaveChanges(); replacement = customer.Id;
            var link = seed.CustomerOwnedVehicles.Single(x => x.VehicleId == a.VehicleId);
            if (scenario.Contains("duplicate") || scenario == "two-owners") seed.Add(new CustomerOwnedVehicles
            { GarageBusinessId = a.GarageId, GarageBusinessCustomerId = scenario == "two-owners" ? replacement : a.CustomerId, VehicleId = a.VehicleId, CreatedDate = DateTime.UtcNow });
            if (scenario.Contains("foreign-label")) link.GarageBusinessId = b.GarageId;
            if (scenario == "foreign-owner") link.GarageBusinessCustomerId = b.CustomerId;
            if (scenario == "dangling-owner") link.GarageBusinessCustomerId = int.MaxValue;
            if (scenario.Contains("contradictory")) seed.CustomerVehicle.Single(x => x.Id == a.VehicleId).GarageOwned = true;
            if (scenario == "delete-foreign-vehicle") link.VehicleId = b.VehicleId;
            if (scenario == "delete-dangling-vehicle") link.VehicleId = int.MaxValue;
            if (scenario == "from-garage") { seed.Remove(link); seed.CustomerVehicle.Single(x => x.Id == a.VehicleId).GarageOwned = true; }
            if (scenario.StartsWith("delete-invoice") || scenario == "delete-legacy-invoice") seed.Add(new VehicleInvoice
            { GarageBusinessId = a.GarageId, CustomerId = scenario == "delete-legacy-invoice" ? null : a.CustomerId,
              GarageBusinessCustomerId = scenario == "delete-legacy-invoice" ? a.CustomerId : null, CreatedDate = DateTime.UtcNow, Paid = true });
            if (scenario == "delete-quote") seed.Add(new WorkQuote { GarageBusinessCustomerId = b.GarageId, CustomerId = a.CustomerId, CreatedDate = DateTime.UtcNow });
            if (scenario == "delete-item") seed.Add(new WorkItem { GarageBusinessCustomerId = a.GarageId, CustomerId = a.CustomerId, VehicleId = a.VehicleId, CreatedDate = DateTime.UtcNow });
            seed.SaveChanges();
        }
        using var db = new OwnershipFaultContext(options) { Fail = scenario.Contains("fail"), After = scenario.EndsWith("after") };
        var before = Snapshot(db); var isDelete = scenario.StartsWith("delete");
        // Seed stale unchanged entities: authoritative locked snapshots must refresh them.
        db.CustomerVehicle.Single(x => x.Id == a.VehicleId);
        db.CustomerOwnedVehicles.AsQueryable().Where(x => x.VehicleId == a.VehicleId).ToList();
        ServiceResult Save() => isDelete
            ? new GarageCustomersService(db, null).DeleteGarageCustomer(scenario == "delete-foreign-target" ? b.CustomerId : a.CustomerId, a.GarageId)
            : new CustomerVehicleService(db, null).AddOrUpdateCustomerVehicle(Input(scenario == "foreign-target" ? b.VehicleId : a.VehicleId,
                scenario == "to-garage" ? "0" : (scenario == "foreign-selected" ? b.CustomerId : replacement).ToString()), a.GarageId, "actor");
        if (db.Fail)
        {
            Assert.ThrowsException<DbUpdateException>(() => Save());
            Assert.IsFalse(db.ChangeTracker.HasChanges());
            db.Fail = false; db.SaveChanges();
            if (relational) { using var check = new SignupContext(options); Assert.AreEqual(before, Snapshot(check)); }
            return;
        }
        var expected = scenario is "reassign" or "to-garage" or "from-garage" or "delete";
        var result = Save(); Assert.AreEqual(expected, result.Success, result.ErrorMessage);
        using var read = new SignupContext(options);
        if (!expected) { Assert.AreEqual(before, Snapshot(read)); Assert.AreEqual(0, db.Saves); return; }
        if (isDelete)
        {
            Assert.IsFalse(read.GarageBusinessCustomer.Any(x => x.Id == a.CustomerId));
            Assert.IsFalse(read.CustomerOwnedVehicles.Any(x => x.GarageBusinessCustomerId == a.CustomerId));
            Assert.IsTrue(read.CustomerVehicle.Any(x => x.Id == a.VehicleId));
        }
        else if (scenario == "to-garage")
        { Assert.IsTrue(read.CustomerVehicle.Single(x => x.Id == a.VehicleId).GarageOwned == true); Assert.IsFalse(read.CustomerOwnedVehicles.Any(x => x.VehicleId == a.VehicleId)); }
        else Assert.AreEqual(replacement, read.CustomerOwnedVehicles.Single(x => x.VehicleId == a.VehicleId).GarageBusinessCustomerId);
        Assert.AreEqual(b.CustomerId, read.CustomerOwnedVehicles.Single(x => x.VehicleId == b.VehicleId).GarageBusinessCustomerId);
    }
    private sealed class OwnershipFaultContext(DbContextOptions<SignupContext> options) : SignupContext(options)
    {
        internal bool Fail, After; internal int Saves;
        public override int SaveChanges()
        {
            Saves++; if (Fail && !After) throw new DbUpdateException("Injected before write");
            var count = base.SaveChanges(); if (Fail && After) throw new DbUpdateException("Injected after write"); return count;
        }
    }
}


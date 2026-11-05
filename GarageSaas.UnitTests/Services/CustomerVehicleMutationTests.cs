using System.Text.Json;
using GarageSaas.Services;
using GarageSaas.Services.Models;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class CustomerVehicleMutationTests
{
    private static readonly InMemoryDatabaseRoot Root = new();
    [DataTestMethod]
    [DataRow("inline-create", 0, false)] [DataRow("existing-create", 0, false)]
    [DataRow("garage-create", 0, false)] [DataRow("inline-update", 0, false)]
    [DataRow("existing-update", 0, false)] [DataRow("customer-create", 0, false)]
    [DataRow("customer-update", 0, false)] [DataRow("combined-create", 0, false)]
    [DataRow("foreign-vehicle", 0, false)] [DataRow("missing-vehicle", 0, false)]
    [DataRow("foreign-owner", 0, false)] [DataRow("foreign-customer", 0, false)]
    [DataRow("missing-owner", 0, false)] [DataRow("negative-vehicle", 0, false)]
    [DataRow("missing-customer", 0, false)] [DataRow("combined-existing-vehicle", 0, false)]
    [DataRow("inline-create", 1, false)] [DataRow("inline-create", 1, true)]
    [DataRow("inline-create", 2, false)] [DataRow("inline-create", 2, true)]
    [DataRow("inline-create", 3, false)] [DataRow("inline-create", 3, true)]
    [DataRow("combined-create", 1, false)] [DataRow("combined-create", 1, true)]
    [DataRow("combined-create", 2, false)] [DataRow("combined-create", 2, true)]
    [DataRow("combined-create", 3, false)] [DataRow("combined-create", 3, true)]
    [DataRow("inline-update", 2, true)] [DataRow("existing-update", 1, true)]
    [DataRow("customer-create", 1, true)] [DataRow("customer-update", 1, true)]
    [DataRow("garage-update", 0, false)] [DataRow("garage-update", 1, true)]
    [DataRow("pending", 0, false)] [DataRow("commit-uncertainty", 0, false)]
    public void MutationOrderingAndCleanup(string operation, int failAt, bool after)
    {
        var options = new DbContextOptionsBuilder<SignupContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), Root)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options;
        CustomerVehicleMutationCases.Check(options, operation, failAt, after, false);
    }
}

internal static class CustomerVehicleMutationCases
{
    internal static void Check(DbContextOptions<SignupContext> options, string operation, int failAt, bool after, bool relational,
        Action<SqlCleanupDiagnostics.SeedStage,Exception?>? observe = null)
    {
        var stage=SqlCleanupDiagnostics.SeedStage.MutationSeed;
        void Note(SqlCleanupDiagnostics.SeedStage value) { stage=value; }
        try { CheckCore(options,operation,failAt,after,relational,Note); try { observe?.Invoke(stage,null); } catch { } }
        catch(Exception error) { try { observe?.Invoke(error is AssertFailedException ? SqlCleanupDiagnostics.SeedStage.MutationAssertions : stage,error); } catch { } throw; }
    }
    private static void CheckCore(DbContextOptions<SignupContext> options, string operation, int failAt, bool after, bool relational,
        Action<SqlCleanupDiagnostics.SeedStage> note)
    {
        SqlInvoiceGraph a, b;
        var seed = new SignupContext(options);
        using (var seedLifetime = new ObservedDisposal(seed,note)) { a = SqlInvoiceGraph.Seed(seed); b = SqlInvoiceGraph.Seed(seed); }
        note(SqlCleanupDiagnostics.SeedStage.MutationSetup);
        var db = new FaultContext(options) { FailAt = failAt, FailAfter = after, LoseCommit = operation == "commit-uncertainty" };
        using var dbLifetime=new ObservedDisposal(db,note);
        var before = Snapshot(db); var foreignBefore = Snapshot(db, b.GarageId);
        var customersBefore = db.GarageBusinessCustomer.Count(); var vehiclesBefore = db.CustomerVehicle.Count(); var linksBefore = db.CustomerOwnedVehicles.Count();
        var customer = new GarageBusinessCustomer { GarageBusinessId = b.GarageId, GarageCustomerForename = "New local",
            GarageCustomerEmailAddress = "local@test.invalid", CreatedBy = "forged", Blocked = true,
            GarageBusiness = new GarageBusiness { GarageBusinessName = "Must never be inserted" } };
        var vehicle = new CustomerVehicle { GarageBusinessId = b.GarageId, VehicleRegistration = "NEW LOCAL",
            VehicleMileageDate = DateTime.UtcNow, CreatedBy = "forged", Blocked = true,
            GarageBusiness = new GarageBusiness { GarageBusinessName = "Must never be inserted" },
            VehicleMake = new VehicleMake { Make = "Must never be inserted" } };
        var vm = new VehicleAndCustomers { Vehicle = vehicle,
            GarageVehicleOwnerListItem = new SelectListItem { Value = a.CustomerId.ToString() } };
        if (operation is "inline-create" or "inline-update" or "foreign-vehicle" or "missing-vehicle")
            vm.NewCustomer = new NewCustomerInput { Forename = "Inline", EmailAddress = "inline@test.invalid" };
        if (operation is "inline-update" or "existing-update" or "garage-update") vehicle.Id = a.VehicleId;
        if (operation == "foreign-vehicle") vehicle.Id = b.VehicleId;
        if (operation == "missing-vehicle") vehicle.Id = int.MaxValue;
        if (operation == "negative-vehicle") vehicle.Id = -1;
        if (operation == "foreign-owner") vm.GarageVehicleOwnerListItem.Value = b.CustomerId.ToString();
        if (operation == "missing-owner") vm.GarageVehicleOwnerListItem.Value = int.MaxValue.ToString();
        if (operation is "garage-create" or "garage-update") vm.GarageVehicleOwnerListItem.Value = "0";
        if (operation == "customer-update") customer.Id = a.CustomerId;
        if (operation == "foreign-customer") customer.Id = b.CustomerId;
        if (operation == "missing-customer") customer.Id = int.MaxValue;
        if (operation == "combined-existing-vehicle") vehicle.Id = b.VehicleId;
        var combined = new GarageCustomerWithVehicleVM { Customer = customer, Vehicle = vehicle, AddVehicle = true };
        ServiceResult SaveCore() => operation.StartsWith("combined")
            ? new GarageCustomersService(db, null).AddGarageCustomerWithVehicle(combined, a.GarageId, "actor")
            : operation.Contains("customer") || operation is "pending" or "commit-uncertainty"
                ? new GarageCustomersService(db, null).AddOrUpdateGarageCustomer(customer, a.GarageId, "actor")
                : new CustomerVehicleService(db, null).AddOrUpdateCustomerVehicle(vm, a.GarageId, "actor");
        ServiceResult Save() { note(SqlCleanupDiagnostics.SeedStage.MutationService); var result=SaveCore(); note(SqlCleanupDiagnostics.SeedStage.MutationAssertions); return result; }
        if (operation == "pending")
        {
            var pending = new GarageBusinessCustomer { GarageBusinessId = a.GarageId, GarageCustomerForename = "Unrelated" };
            db.GarageBusinessCustomer.Add(pending);
            Assert.IsFalse(Save().Success); Assert.AreEqual(EntityState.Added, db.Entry(pending).State);
            Assert.AreEqual(0, db.Saves); return;
        }
        var invalid = operation is "foreign-vehicle" or "missing-vehicle" or "negative-vehicle" or "foreign-owner" or "missing-owner" or "foreign-customer" or "missing-customer" or "combined-existing-vehicle";
        if (operation == "commit-uncertainty")
        {
            var error = Assert.ThrowsException<InvalidOperationException>(() => Save());
            StringAssert.Contains(error.Message, "could not be confirmed");
            Assert.AreEqual(1, db.Commits); Assert.AreEqual(1, db.Saves); Assert.AreEqual(0, customer.Id);
            Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
            var committed = new SignupContext(options); using var committedLifetime=new ObservedDisposal(committed,note);
            Assert.AreEqual(customersBefore + 1, committed.GarageBusinessCustomer.Count());
            return; // Actual commit succeeded; rollback acknowledgement cannot undo it.
        }
        if (failAt > 0)
        {
            Assert.ThrowsException<DbUpdateException>(() => Save());
            Assert.AreEqual(failAt, db.Saves); Assert.AreEqual(0, db.Commits);
            Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
            foreach (var entity in db.Created)
            {
                var id = entity is GarageBusinessCustomer c ? c.Id : entity is CustomerVehicle v ? v.Id : ((CustomerOwnedVehicles)entity).Id;
                Assert.AreEqual(0, id, "Rolled-back generated identities must not be reused");
            }
            Assert.AreEqual(operation == "customer-update" ? a.CustomerId : 0, customer.Id);
            db.FailAt = 0; db.SaveChanges(); // Cannot re-persist detached rolled-back entities.
            if (relational)
            {
                var restored = new SignupContext(options); using var restoredLifetime=new ObservedDisposal(restored,note);
                Assert.AreEqual(before, Snapshot(restored), "Real SQL rollback must restore values, audit and row counts");
            }
        }
        else
        {
            var result = Save(); Assert.AreEqual(!invalid, result.Success, result.ErrorMessage);
            var check = new SignupContext(options); using var checkLifetime=new ObservedDisposal(check,note);
            if (invalid)
            {
                Assert.AreEqual(0, db.Saves); Assert.AreEqual(before, Snapshot(check));
                Assert.IsFalse(db.ChangeTracker.HasChanges());
            }
            else
            {
                var newCustomer = operation is "inline-create" or "inline-update" or "customer-create" or "combined-create";
                var newVehicle = operation is "inline-create" or "existing-create" or "garage-create" or "combined-create";
                var newLink = operation is "inline-create" or "existing-create" or "combined-create";
                Assert.AreEqual(customersBefore + (newCustomer ? 1 : 0), check.GarageBusinessCustomer.Count());
                Assert.AreEqual(vehiclesBefore + (newVehicle ? 1 : 0), check.CustomerVehicle.Count());
                Assert.AreEqual(linksBefore + (newLink ? 1 : 0) - (operation == "garage-update" ? 1 : 0), check.CustomerOwnedVehicles.Count());
                Assert.AreEqual(1, db.Commits);
                if (relational) { Assert.AreEqual(1, db.TransactionIds.Distinct().Count()); Assert.IsFalse(db.TransactionIds.Contains(Guid.Empty)); }
                Assert.IsTrue(check.GarageBusinessCustomer.AsNoTracking().Where(x => x.Id > Math.Max(a.CustomerId,b.CustomerId)).All(x => x.GarageBusinessId == a.GarageId && !x.Blocked && x.CreatedBy == "actor"));
                Assert.IsTrue(check.CustomerVehicle.AsNoTracking().Where(x => x.Id > Math.Max(a.VehicleId,b.VehicleId)).All(x => x.GarageBusinessId == a.GarageId && !x.Blocked && x.CreatedBy == "actor"));
                Assert.AreEqual(2, check.GarageBusiness.Count()); Assert.AreEqual(0, check.VehicleMake.Count());
                if (operation == "garage-create") Assert.IsTrue(check.CustomerVehicle.Single(x => x.VehicleRegistration == "NEW LOCAL").GarageOwned == true);
                if (operation == "inline-update")
                    Assert.AreNotEqual(a.CustomerId, check.CustomerOwnedVehicles.Single(x => x.VehicleId == a.VehicleId).GarageBusinessCustomerId);
                if (operation.StartsWith("customer") && operation != "customer-update") Assert.IsTrue(customer.Id > 0);
                if (operation == "combined-create") { Assert.IsTrue(customer.Id > 0); Assert.IsTrue(vehicle.Id > 0); }
            }
        }
        var foreign = new SignupContext(options); using var foreignLifetime=new ObservedDisposal(foreign,note);
        Assert.AreEqual(foreignBefore, Snapshot(foreign, b.GarageId), "Garage B must remain unchanged");
        note(SqlCleanupDiagnostics.SeedStage.MutationDispose);
    }

    private sealed class ObservedDisposal(IDisposable resource,Action<SqlCleanupDiagnostics.SeedStage> note) : IDisposable
    {
        public void Dispose() { try { resource.Dispose(); } catch { note(SqlCleanupDiagnostics.SeedStage.MutationDispose); throw; } }
    }
    private static string Snapshot(SignupContext db, int? garage = null) => JsonSerializer.Serialize(new
    {
        Customers = db.GarageBusinessCustomer.AsNoTracking().Where(x => !garage.HasValue || x.GarageBusinessId == garage).OrderBy(x => x.Id)
            .Select(x => new { x.Id,x.GarageBusinessId,x.GarageCustomerForename,x.GarageCustomerEmailAddress,x.CreatedBy,x.CreatedDate,x.UpdatedBy,x.UpdatedDate }).ToList(),
        Vehicles = db.CustomerVehicle.AsNoTracking().Where(x => !garage.HasValue || x.GarageBusinessId == garage).OrderBy(x => x.Id)
            .Select(x => new { x.Id,x.GarageBusinessId,x.VehicleRegistration,x.GarageOwned,x.CreatedBy,x.CreatedDate,x.UpdatedBy,x.UpdatedDate }).ToList(),
        Links = db.CustomerOwnedVehicles.AsNoTracking().Where(x => !garage.HasValue || x.GarageBusinessId == garage).OrderBy(x => x.Id)
            .Select(x => new { x.Id,x.GarageBusinessId,x.VehicleId,x.GarageBusinessCustomerId,x.CreatedBy,x.CreatedDate,x.UpdatedBy,x.UpdatedDate }).ToList()
    });

    private sealed class FaultContext : SignupContext
    {
        private readonly Mock<DatabaseFacade> _facade;
        public int FailAt, Saves, Commits;
        public bool FailAfter, LoseCommit;
        public List<Guid> TransactionIds { get; } = new();
        public HashSet<object> Created { get; } = new();
        public override DatabaseFacade Database => _facade?.Object ?? base.Database;
        public FaultContext(DbContextOptions<SignupContext> options) : base(options)
        {
            _facade = new Mock<DatabaseFacade>(this) { CallBase = true };
            _facade.Setup(x => x.BeginTransaction()).Returns(() =>
            {
                var real = base.Database.BeginTransaction(); var proxy = new Mock<IDbContextTransaction>();
                proxy.Setup(x => x.Commit()).Callback(() => { Commits++; real.Commit(); if (LoseCommit) throw new IOException("Injected commit acknowledgement loss"); });
                proxy.Setup(x => x.Rollback()).Callback(() => real.Rollback()); proxy.Setup(x => x.Dispose()).Callback(() => real.Dispose());
                return proxy.Object;
            });
        }
        public override int SaveChanges()
        {
            Saves++; TransactionIds.Add(base.Database.CurrentTransaction?.TransactionId ?? Guid.Empty);
            foreach (var e in ChangeTracker.Entries().Where(x => x.State == EntityState.Added)) Created.Add(e.Entity);
            if (Saves == FailAt && !FailAfter) throw new DbUpdateException("Injected before save");
            var result = base.SaveChanges();
            if (Saves == FailAt && FailAfter) throw new DbUpdateException("Injected after save");
            return result;
        }
    }
}

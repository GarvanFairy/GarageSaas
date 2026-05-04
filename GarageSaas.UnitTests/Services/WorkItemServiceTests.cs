using System;
using System.Linq;
using GarageSaas.Services;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.Tests.Services
{
    [TestClass]
    public class WorkItemServiceTests
    {
        [TestMethod]
        public void GetWorkItem_Returns_Fail_When_Not_Found()
        {
            using var context = DbContextFactory.Create();

            var service = new WorkItemService(context);

            var result = service.GetWorkItem(999, 5);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("WorkItem not found.", result.ErrorMessage);
        }

        [TestMethod]
        public void GetWorkItem_Returns_WorkItem_When_Found()
        {
            using var context = DbContextFactory.Create();

            context.WorkItem.Add(new WorkItem
            {
                Id = 1,
                GarageBusinessCustomerId = 5,
                VehicleId = 100,
                CustomerId = 10,
                RepairInstructions = "Replace brake pads",
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.SaveChanges();

            var service = new WorkItemService(context);

            var result = service.GetWorkItem(1, 5);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual("Replace brake pads", result.Data.RepairInstructions);
            Assert.AreEqual(100, result.Data.VehicleId);
        }

        [TestMethod]
        public void GetWorkItem_Returns_Fail_When_GarageBusiness_Does_Not_Match()
        {
            using var context = DbContextFactory.Create();

            context.WorkItem.Add(new WorkItem
            {
                Id = 1,
                GarageBusinessCustomerId = 5,
                VehicleId = 100,
                CustomerId = 10,
                RepairInstructions = "Replace brake pads",
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.SaveChanges();

            var service = new WorkItemService(context);

            var result = service.GetWorkItem(1, 99);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("WorkItem not found.", result.ErrorMessage);
        }

        [TestMethod]
        public void GetWorkItemsForVehicle_Returns_Only_WorkItems_For_Vehicle_And_Garage()
        {
            using var context = DbContextFactory.Create();

            context.WorkItem.Add(new WorkItem
            {
                Id = 1,
                GarageBusinessCustomerId = 5,
                VehicleId = 100,
                CustomerId = 10,
                RepairInstructions = "Vehicle 100 item",
                CreatedDate = DateTime.Now.AddDays(-1),
                CreatedBy = "seed"
            });

            context.WorkItem.Add(new WorkItem
            {
                Id = 2,
                GarageBusinessCustomerId = 5,
                VehicleId = 200,
                CustomerId = 10,
                RepairInstructions = "Vehicle 200 item",
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.WorkItem.Add(new WorkItem
            {
                Id = 3,
                GarageBusinessCustomerId = 99,
                VehicleId = 100,
                CustomerId = 10,
                RepairInstructions = "Wrong garage item",
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.SaveChanges();

            var service = new WorkItemService(context);

            var result = service.GetWorkItemsForVehicle(100, 5);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Data.Count);
            Assert.AreEqual("Vehicle 100 item", result.Data[0].RepairInstructions);
        }

        [TestMethod]
        public void AddOrUpdateWorkItem_Returns_Fail_When_Model_Is_Null()
        {
            using var context = DbContextFactory.Create();

            var service = new WorkItemService(context);

            var result = service.AddOrUpdateWorkItem(null, 5, "tester");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("WorkItem is null.", result.ErrorMessage);
        }

        [TestMethod]
        public void AddOrUpdateWorkItem_Returns_Fail_When_VehicleId_Is_Missing()
        {
            using var context = DbContextFactory.Create();

            var service = new WorkItemService(context);

            var result = service.AddOrUpdateWorkItem(new WorkItem
            {
                RepairInstructions = "Replace clutch"
            }, 5, "tester");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("VehicleId is required.", result.ErrorMessage);
        }

        [TestMethod]
        public void AddOrUpdateWorkItem_Adds_New_WorkItem()
        {
            using var context = DbContextFactory.Create();

            var service = new WorkItemService(context);

            var model = new WorkItem
            {
                VehicleId = 100,
                CustomerId = 10,
                RepairInstructions = "Replace clutch"
            };

            var result = service.AddOrUpdateWorkItem(model, 5, "tester");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, context.WorkItem.Count());

            var saved = context.WorkItem.Single();

            Assert.AreEqual(5, saved.GarageBusinessCustomerId);
            Assert.AreEqual(100, saved.VehicleId);
            Assert.AreEqual(10, saved.CustomerId);
            Assert.AreEqual("Replace clutch", saved.RepairInstructions);
            Assert.AreEqual("tester", saved.CreatedBy);
            Assert.AreNotEqual(default, saved.CreatedDate);
        }

        [TestMethod]
        public void AddOrUpdateWorkItem_Updates_Existing_WorkItem()
        {
            using var context = DbContextFactory.Create();

            context.WorkItem.Add(new WorkItem
            {
                Id = 1,
                GarageBusinessCustomerId = 5,
                VehicleId = 100,
                CustomerId = 10,
                RepairInstructions = "Old instructions",
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.SaveChanges();

            var service = new WorkItemService(context);

            var update = new WorkItem
            {
                Id = 1,
                VehicleId = 200,
                CustomerId = 20,
                RepairInstructions = "Updated instructions"
            };

            var result = service.AddOrUpdateWorkItem(update, 5, "editor");

            Assert.IsTrue(result.Success);

            var saved = context.WorkItem.Single(x => x.Id == 1);

            Assert.AreEqual(5, saved.GarageBusinessCustomerId);
            Assert.AreEqual(200, saved.VehicleId);
            Assert.AreEqual(20, saved.CustomerId);
            Assert.AreEqual("Updated instructions", saved.RepairInstructions);
            Assert.AreEqual("editor", saved.UpdatedBy);
            Assert.IsNotNull(saved.UpdatedDate);
        }

        [TestMethod]
        public void AddOrUpdateWorkItem_Returns_Fail_When_Update_Target_Not_Found()
        {
            using var context = DbContextFactory.Create();

            var service = new WorkItemService(context);

            var update = new WorkItem
            {
                Id = 999,
                VehicleId = 100,
                CustomerId = 10,
                RepairInstructions = "Updated instructions"
            };

            var result = service.AddOrUpdateWorkItem(update, 5, "editor");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("WorkItem not found.", result.ErrorMessage);
        }
    }
}
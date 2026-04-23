using System;
using System.Linq;
using GarageSaas.Services;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.Tests.Services
{
    [TestClass]
    public class VehiclePartServiceTests
    {
        [TestMethod]
        public void GetVehiclePart_Returns_Fail_When_Not_Found()
        {
            using var context = DbContextFactory.Create();

            var service = new VehiclePartService(context);

            var result = service.GetVehiclePart(999);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Vehicle part not found.", result.ErrorMessage);
        }

        [TestMethod]
        public void GetVehiclePart_Returns_Part_When_Found()
        {
            using var context = DbContextFactory.Create();

            context.VehiclePart.Add(new VehiclePart
            {
                Id = 1,
                VehiclePartName = "Brake Pad",
                PartNumber = "BP-001",
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.SaveChanges();

            var service = new VehiclePartService(context);

            var result = service.GetVehiclePart(1);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual("Brake Pad", result.Data.VehiclePartName);
            Assert.AreEqual("BP-001", result.Data.PartNumber);
        }

        [TestMethod]
        public void GetVehicleParts_Returns_All_Parts_Ordered_By_Name()
        {
            using var context = DbContextFactory.Create();

            context.VehiclePart.Add(new VehiclePart
            {
                Id = 1,
                VehiclePartName = "Z Part",
                CreatedDate = DateTime.Now
            });

            context.VehiclePart.Add(new VehiclePart
            {
                Id = 2,
                VehiclePartName = "A Part",
                CreatedDate = DateTime.Now
            });

            context.SaveChanges();

            var service = new VehiclePartService(context);

            var result = service.GetVehicleParts();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Data.Count);
            Assert.AreEqual("A Part", result.Data[0].VehiclePartName);
            Assert.AreEqual("Z Part", result.Data[1].VehiclePartName);
        }

        [TestMethod]
        public void AddOrUpdateVehiclePart_Returns_Fail_When_Model_Is_Null()
        {
            using var context = DbContextFactory.Create();

            var service = new VehiclePartService(context);

            var result = service.AddOrUpdateVehiclePart(null, "tester");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Vehicle part is null.", result.ErrorMessage);
        }

        [TestMethod]
        public void AddOrUpdateVehiclePart_Returns_Fail_When_Name_Is_Missing()
        {
            using var context = DbContextFactory.Create();

            var service = new VehiclePartService(context);

            var result = service.AddOrUpdateVehiclePart(new VehiclePart
            {
                VehiclePartName = "",
                PartNumber = "BP-001"
            }, "tester");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Vehicle part name is required.", result.ErrorMessage);
        }

        [TestMethod]
        public void AddOrUpdateVehiclePart_Adds_New_Part()
        {
            using var context = DbContextFactory.Create();

            var service = new VehiclePartService(context);

            var part = new VehiclePart
            {
                VehiclePartName = "Brake Pad",
                PartNumber = "BP-001",
                VehiclePartDescription = "Front brake pad",
                VehiclePartPrice = "45.00",
                VehiclePartQuantity = "2",
                VehiclePartSupplier = "Parts Supplier"
            };

            var result = service.AddOrUpdateVehiclePart(part, "tester");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, context.VehiclePart.Count());

            var saved = context.VehiclePart.Single();

            Assert.AreEqual("Brake Pad", saved.VehiclePartName);
            Assert.AreEqual("BP-001", saved.PartNumber);
            Assert.AreEqual("Front brake pad", saved.VehiclePartDescription);
            Assert.AreEqual("45.00", saved.VehiclePartPrice);
            Assert.AreEqual("2", saved.VehiclePartQuantity);
            Assert.AreEqual("Parts Supplier", saved.VehiclePartSupplier);
            Assert.AreEqual("tester", saved.CreatedBy);
            Assert.AreNotEqual(default, saved.CreatedDate);
        }

        [TestMethod]
        public void AddOrUpdateVehiclePart_Updates_Existing_Part()
        {
            using var context = DbContextFactory.Create();

            context.VehiclePart.Add(new VehiclePart
            {
                Id = 1,
                VehiclePartName = "Old Part",
                PartNumber = "OLD-001",
                VehiclePartDescription = "Old description",
                VehiclePartPrice = "10.00",
                VehiclePartQuantity = "1",
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.SaveChanges();

            var service = new VehiclePartService(context);

            var update = new VehiclePart
            {
                Id = 1,
                VehiclePartName = "Updated Brake Pad",
                PartNumber = "BP-002",
                VehiclePartDescription = "Updated description",
                VehiclePartPrice = "55.00",
                VehiclePartQuantity = "4",
                VehiclePartSupplier = "New Supplier"
            };

            var result = service.AddOrUpdateVehiclePart(update, "editor");

            Assert.IsTrue(result.Success);

            var saved = context.VehiclePart.Single(x => x.Id == 1);

            Assert.AreEqual("Updated Brake Pad", saved.VehiclePartName);
            Assert.AreEqual("BP-002", saved.PartNumber);
            Assert.AreEqual("Updated description", saved.VehiclePartDescription);
            Assert.AreEqual("55.00", saved.VehiclePartPrice);
            Assert.AreEqual("4", saved.VehiclePartQuantity);
            Assert.AreEqual("New Supplier", saved.VehiclePartSupplier);
            Assert.AreEqual("editor", saved.UpdatedBy);
            Assert.IsNotNull(saved.UpdatedDate);
        }

        [TestMethod]
        public void AddOrUpdateVehiclePart_Returns_Fail_When_Update_Part_Not_Found()
        {
            using var context = DbContextFactory.Create();

            var service = new VehiclePartService(context);

            var update = new VehiclePart
            {
                Id = 999,
                VehiclePartName = "Missing Part"
            };

            var result = service.AddOrUpdateVehiclePart(update, "editor");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Vehicle part not found.", result.ErrorMessage);
        }

        [TestMethod]
        public void DeleteVehiclePart_Removes_Part()
        {
            using var context = DbContextFactory.Create();

            context.VehiclePart.Add(new VehiclePart
            {
                Id = 1,
                VehiclePartName = "Brake Pad",
                CreatedDate = DateTime.Now
            });

            context.SaveChanges();

            var service = new VehiclePartService(context);

            var result = service.DeleteVehiclePart(1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, context.VehiclePart.Count());
        }

        [TestMethod]
        public void DeleteVehiclePart_Returns_Fail_When_Not_Found()
        {
            using var context = DbContextFactory.Create();

            var service = new VehiclePartService(context);

            var result = service.DeleteVehiclePart(999);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Vehicle part not found.", result.ErrorMessage);
        }
    }
}
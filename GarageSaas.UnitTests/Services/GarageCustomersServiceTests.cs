using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.UnitTests.TestHelpers;
using global::GarageSaas.Services;
using global::GarageSaas.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services
{

        [TestClass]
        public class GarageCustomersServiceTests
        {
            private Mock<IVehicleLookupService> BuildLookupMock()
            {
                var mock = new Mock<IVehicleLookupService>();

                mock.Setup(x => x.GetVehicleMakesAsync())
                    .ReturnsAsync(new List<SelectListItem>());

                mock.Setup(x => x.GetVehicleModelsAsync())
                    .ReturnsAsync(new List<SelectListItem>());

                mock.Setup(x => x.GetFuelTypesAsync())
                    .ReturnsAsync(new List<SelectListItem>());

                mock.Setup(x => x.GetVehicleYearsAsync())
                    .ReturnsAsync(new List<SelectListItem>());

                return mock;
            }

            [TestMethod]
            public void AddOrUpdateGarageCustomer_Adds_New_Customer()
            {
            using var context = DbContextFactory.Create();
            var lookupMock = BuildLookupMock();

                var service = new GarageCustomersService(context, lookupMock.Object);

                var customer = new GarageBusinessCustomer
                {
                    GarageCustomerForename = "John",
                    GarageCustomerSurname = "Murphy",
                    GarageCustomerEmailAddress = "john@test.com"
                };

                var result = service.AddOrUpdateGarageCustomer(customer, 5, "tester");

                Assert.IsTrue(result.Success);

                var saved = context.GarageBusinessCustomer.Single();
                Assert.AreEqual("John", saved.GarageCustomerForename);
                Assert.AreEqual(5, saved.GarageBusinessId);
                Assert.AreEqual("tester", saved.CreatedBy);
            }

            [TestMethod]
            public void AddOrUpdateGarageCustomer_Updates_Existing_Customer()
            {
            using var context = DbContextFactory.Create();
            var lookupMock = BuildLookupMock();

                context.GarageBusinessCustomer.Add(new GarageBusinessCustomer
                {
                    Id = 1,
                    GarageBusinessId = 5,
                    GarageCustomerForename = "Old",
                    GarageCustomerSurname = "Name",
                    CreatedDate = DateTime.Now,
                    CreatedBy = "seed",
                    Active = true
                });

                context.SaveChanges();

                var service = new GarageCustomersService(context, lookupMock.Object);

                var customer = new GarageBusinessCustomer
                {
                    Id = 1,
                    GarageCustomerForename = "New",
                    GarageCustomerSurname = "Surname",
                    GarageCustomerEmailAddress = "new@test.com"
                };

                var result = service.AddOrUpdateGarageCustomer(customer, 5, "editor");

                Assert.IsTrue(result.Success);

                var saved = context.GarageBusinessCustomer.Single(x => x.Id == 1);
                Assert.AreEqual("New", saved.GarageCustomerForename);
                Assert.AreEqual("Surname", saved.GarageCustomerSurname);
                Assert.AreEqual("editor", saved.UpdatedBy);
            }

            [TestMethod]
            public void GetGarageCustomerForEdit_Returns_Customer_And_Vehicles()
            {
            using var context = DbContextFactory.Create();
            var lookupMock = BuildLookupMock();

                context.GarageBusinessCustomer.Add(new GarageBusinessCustomer
                {
                    Id = 1,
                    GarageBusinessId = 5,
                    GarageCustomerForename = "John",
                    GarageCustomerSurname = "Murphy"
                });

                context.VehicleMake.Add(new VehicleMake
                {
                    Id = 1,
                    Make = "Ford",
                    Active = true
                });

                context.VehicleModel.Add(new VehicleModel
                {
                    Id = 2,
                    MakeId = 1,
                    Model = "Focus",
                    Active = true
                });

                context.CustomerVehicle.Add(new CustomerVehicle
                {
                    Id = 100,
                    VehicleMakeId = 1,
                    VehicleModelId = 2,
                    VehicleRegistration = "10-D-12345",
                    GarageOwned = false,
                    GarageBusinessId = 5,
                    CreatedDate = DateTime.Now,
                    Active = true
                });

                context.CustomerOwnedVehicles.Add(new CustomerOwnedVehicles
                {
                    Id = 1000,
                    GarageBusinessCustomerId = 1,
                    VehicleId = 100,
                    GarageBusinessId = 5,
                    CreatedDate = DateTime.Now,
                    Active = true
                });

                context.SaveChanges();

                var service = new GarageCustomersService(context, lookupMock.Object);

                var result = service.GetGarageCustomerForEdit(1);

                Assert.IsTrue(result.Success);
                Assert.IsNotNull(result.Data);
                Assert.AreEqual(1, result.Data.Customer.Id);
                Assert.AreEqual(1, result.Data.Vehicles.Count);
                Assert.AreEqual("Ford", result.Data.Vehicles[0].Make);
                Assert.AreEqual("Focus", result.Data.Vehicles[0].Model);
            }

            [TestMethod]
            public async Task BuildAddCustomerWithVehicleVmAsync_Returns_Lookups()
            {
            using var context = DbContextFactory.Create();
            var lookupMock = BuildLookupMock();

                lookupMock.Setup(x => x.GetVehicleMakesAsync())
                    .ReturnsAsync(new List<SelectListItem> { new SelectListItem { Value = "1", Text = "Ford" } });

                var service = new GarageCustomersService(context, lookupMock.Object);

                var result = await service.BuildAddCustomerWithVehicleVmAsync();

                Assert.IsTrue(result.Success);
                Assert.IsNotNull(result.Data);
                Assert.IsNotNull(result.Data.Customer);
                Assert.IsNotNull(result.Data.Vehicle);
                Assert.AreEqual(1, result.Data.ListOfVehicleMakes.Count);
            }
        }
    }
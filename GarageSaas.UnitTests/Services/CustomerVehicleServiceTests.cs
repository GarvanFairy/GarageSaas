using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services
{

    [TestClass]
    public class CustomerVehicleServiceTests
    {
        private Mock<IVehicleLookupService> BuildLookupMock()
        {
            var mock = new Mock<IVehicleLookupService>();

            mock.Setup(x => x.GetVehicleMakesAsync())
                .ReturnsAsync(new List<SelectListItem> { new SelectListItem { Value = "1", Text = "Ford" } });

            mock.Setup(x => x.GetVehicleModelsAsync())
                .ReturnsAsync(new List<SelectListItem> { new SelectListItem { Value = "2", Text = "Focus" } });

            mock.Setup(x => x.GetVehicleModelsByMakeAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<SelectListItem> { new SelectListItem { Value = "2", Text = "Focus" } });

            mock.Setup(x => x.GetFuelTypesAsync())
                .ReturnsAsync(new List<SelectListItem> { new SelectListItem { Value = "3", Text = "Diesel" } });

            mock.Setup(x => x.GetVehicleYearsAsync())
                .ReturnsAsync(new List<SelectListItem> { new SelectListItem { Value = "4", Text = "2020" } });

            mock.Setup(x => x.GetMileageAsync())
                .ReturnsAsync(new List<SelectListItem> { new SelectListItem { Value = "5", Text = "50000" } });

            mock.Setup(x => x.GetTransmissionTypesAsync())
                .ReturnsAsync(new List<SelectListItem> { new SelectListItem { Value = "6", Text = "Manual" } });

            return mock;
        }

        private Mock<IVehicleLookupService> BuildVehicleLookupMock()
        {
            var mock = new Mock<IVehicleLookupService>();

            mock.Setup(x => x.GetVehicleMakesAsync())
                .ReturnsAsync(new List<SelectListItem>
                {
            new SelectListItem { Value = "1", Text = "Ford" },
            new SelectListItem { Value = "2", Text = "Toyota" }
                });

            mock.Setup(x => x.GetVehicleModelsAsync())
                .ReturnsAsync(new List<SelectListItem>
                {
            new SelectListItem { Value = "10", Text = "Focus" },
            new SelectListItem { Value = "11", Text = "Corolla" }
                });

            mock.Setup(x => x.GetVehicleModelsByMakeAsync(It.IsAny<int>()))
                .ReturnsAsync((int makeId) =>
                {
                    if (makeId == 1)
                    {
                        return new List<SelectListItem>
                        {
                    new SelectListItem { Value = "10", Text = "Focus" },
                    new SelectListItem { Value = "12", Text = "Fiesta" }
                        };
                    }

                    if (makeId == 2)
                    {
                        return new List<SelectListItem>
                        {
                    new SelectListItem { Value = "11", Text = "Corolla" },
                    new SelectListItem { Value = "13", Text = "Yaris" }
                        };
                    }

                    return new List<SelectListItem>();
                });

            mock.Setup(x => x.GetFuelTypesAsync())
                .ReturnsAsync(new List<SelectListItem>
                {
            new SelectListItem { Value = "20", Text = "Petrol" },
            new SelectListItem { Value = "21", Text = "Diesel" }
                });

            mock.Setup(x => x.GetVehicleYearsAsync())
                .ReturnsAsync(new List<SelectListItem>
                {
            new SelectListItem { Value = "30", Text = "2020" },
            new SelectListItem { Value = "31", Text = "2021" }
                });

            mock.Setup(x => x.GetMileageAsync())
                .ReturnsAsync(new List<SelectListItem>
                {
            new SelectListItem { Value = "40", Text = "50000" },
            new SelectListItem { Value = "41", Text = "75000" }
                });

            mock.Setup(x => x.GetTransmissionTypesAsync())
                .ReturnsAsync(new List<SelectListItem>
                {
            new SelectListItem { Value = "50", Text = "Manual" },
            new SelectListItem { Value = "51", Text = "Automatic" }
                });

            return mock;
        }

        [TestMethod]
        public async Task BuildAddCustomerVehicleVmAsync_Returns_Lookup_Lists()
        {
            using var context = DbContextFactory.Create(nameof(BuildAddCustomerVehicleVmAsync_Returns_Lookup_Lists));
            var lookupMock = BuildLookupMock();

            context.Users.Add(new Users
            {
                Id = 100,
                GarageBusinessId = 5
            });

            context.GarageBusinessCustomer.Add(new GarageBusinessCustomer
            {
                Id = 1,
                GarageBusinessId = 5,
                GarageCustomerForename = "John",
                GarageCustomerSurname = "Murphy",
                CreatedDate = DateTime.Now,
                Active = true
            });

            context.SaveChanges();

            var service = new CustomerVehicleService(context, lookupMock.Object);

            var result = await service.BuildAddCustomerVehicleVmAsync(100, 5);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(1, result.Data.GarageVehicleOwnerList.Count);
            Assert.AreEqual(1, result.Data.ListofVehicleMakes.Count);
            Assert.AreEqual(1, result.Data.ListofVehicleModels.Count);
        }

        [TestMethod]
        public void AddOrUpdateCustomerVehicle_Adds_Vehicle_With_Existing_Customer()
        {
            using var context = DbContextFactory.Create(nameof(AddOrUpdateCustomerVehicle_Adds_Vehicle_With_Existing_Customer));
            var lookupMock = BuildLookupMock();

            context.GarageBusinessCustomer.Add(new GarageBusinessCustomer
            {
                Id = 1,
                GarageBusinessId = 5,
                GarageCustomerForename = "John",
                GarageCustomerSurname = "Murphy",
                CreatedDate = DateTime.Now,
                Active = true
            });

            context.SaveChanges();

            var service = new CustomerVehicleService(context, lookupMock.Object);

            var model = new VehicleAndCustomers
            {
                Vehicle = new CustomerVehicle
                {
                    VehicleRegistration = "10-D-12345",
                    VehicleMileageDate = DateTime.Now
                },
                GarageVehicleOwnerListItem = new SelectListItem { Value = "1", Text = "John Murphy" },
                VehicleMakeListItem = new SelectListItem { Value = "1", Text = "Ford" },
                VehicleModelListItem = new SelectListItem { Value = "2", Text = "Focus" },
                FuelTypeListItem = new SelectListItem { Value = "3", Text = "Diesel" },
                VehicleYearListItem = new SelectListItem { Value = "4", Text = "2020" },
                VehicleMileageListItem = new SelectListItem { Value = "5", Text = "50000" },
                TransmissionListItem = new SelectListItem { Value = "6", Text = "Manual" },
                NCTMonthListItem = new SelectListItem { Value = "JAN", Text = "JAN" },
                NCTYearListItem = new SelectListItem { Value = "2026", Text = "2026" },
                TaxMonthListItem = new SelectListItem { Value = "FEB", Text = "FEB" },
                TaxYearListItem = new SelectListItem { Value = "2026", Text = "2026" }
            };

            var result = service.AddOrUpdateCustomerVehicle(model, 5, "tester");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, context.CustomerVehicle.Count());
            Assert.AreEqual(1, context.CustomerOwnedVehicles.Count());

            var vehicle = context.CustomerVehicle.Single();
            Assert.AreEqual("10-D-12345", vehicle.VehicleRegistration);
            Assert.AreEqual(1, vehicle.VehicleMakeId);
            Assert.AreEqual(false, vehicle.GarageOwned);

            var ownership = context.CustomerOwnedVehicles.Single();
            Assert.AreEqual(1, ownership.GarageBusinessCustomerId);
            Assert.AreEqual(vehicle.Id, ownership.VehicleId);
        }

        [TestMethod]
        public void AddOrUpdateCustomerVehicle_Adds_GarageOwned_Vehicle()
        {
            using var context = DbContextFactory.Create(nameof(AddOrUpdateCustomerVehicle_Adds_GarageOwned_Vehicle));
            var lookupMock = BuildLookupMock();

            var service = new CustomerVehicleService(context, lookupMock.Object);

            var model = new VehicleAndCustomers
            {
                Vehicle = new CustomerVehicle
                {
                    VehicleRegistration = "11-D-99999",
                    VehicleMileageDate = DateTime.Now
                },
                GarageVehicleOwnerListItem = new SelectListItem { Value = "0", Text = "Garage Owner" },
                VehicleMakeListItem = new SelectListItem { Value = "1", Text = "Ford" },
                VehicleModelListItem = new SelectListItem { Value = "2", Text = "Focus" },
                FuelTypeListItem = new SelectListItem { Value = "3", Text = "Diesel" },
                VehicleYearListItem = new SelectListItem { Value = "4", Text = "2020" },
                VehicleMileageListItem = new SelectListItem { Value = "5", Text = "50000" },
                TransmissionListItem = new SelectListItem { Value = "6", Text = "Manual" }
            };

            var result = service.AddOrUpdateCustomerVehicle(model, 5, "tester");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, context.CustomerVehicle.Count());
            Assert.AreEqual(0, context.CustomerOwnedVehicles.Count());

            var vehicle = context.CustomerVehicle.Single();
            Assert.AreEqual(true, vehicle.GarageOwned);
        }

        [TestMethod]
        public void AddOrUpdateCustomerVehicle_Adds_NewCustomer_And_Vehicle()
        {
            using var context = DbContextFactory.Create(nameof(AddOrUpdateCustomerVehicle_Adds_NewCustomer_And_Vehicle));
            var lookupMock = BuildLookupMock();

            var service = new CustomerVehicleService(context, lookupMock.Object);

            var model = new VehicleAndCustomers
            {
                Vehicle = new CustomerVehicle
                {
                    VehicleRegistration = "12-D-11111",
                    VehicleMileageDate = DateTime.Now
                },
                VehicleMakeListItem = new SelectListItem { Value = "1", Text = "Ford" },
                VehicleModelListItem = new SelectListItem { Value = "2", Text = "Focus" },
                FuelTypeListItem = new SelectListItem { Value = "3", Text = "Diesel" },
                VehicleYearListItem = new SelectListItem { Value = "4", Text = "2020" },
                VehicleMileageListItem = new SelectListItem { Value = "5", Text = "50000" },
                TransmissionListItem = new SelectListItem { Value = "6", Text = "Manual" },
                NewCustomer = new NewCustomerInput
                {
                    Forename = "Mary",
                    Surname = "Kelly",
                    MobileNumber = "0871234567",
                    EmailAddress = "mary@test.com"
                }
            };

            var result = service.AddOrUpdateCustomerVehicle(model, 5, "tester");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, context.GarageBusinessCustomer.Count());
            Assert.AreEqual(1, context.CustomerVehicle.Count());
            Assert.AreEqual(1, context.CustomerOwnedVehicles.Count());

            var customer = context.GarageBusinessCustomer.Single();
            Assert.AreEqual("Mary", customer.GarageCustomerForename);

            var ownership = context.CustomerOwnedVehicles.Single();
            Assert.AreEqual(customer.Id, ownership.GarageBusinessCustomerId);
        }

        [TestMethod]
        public void AddCustomer_Should_Save_To_Database()
        {
            using var context = DbContextFactory.Create(nameof(AddCustomer_Should_Save_To_Database));

            var service = new GarageCustomersService(context, null); // mock later

            var customer = new GarageBusinessCustomer
            {
                GarageCustomerForename = "John",
                GarageCustomerSurname = "Murphy",
                GarageCustomerEmailAddress = "john@test.com"
            };

            var result = service.AddOrUpdateGarageCustomer(customer, 1, "tester");

            Assert.IsTrue(result.Success);

            var saved = context.GarageBusinessCustomer.First();

            Assert.AreEqual("John", saved.GarageCustomerForename);
            Assert.AreEqual(1, saved.GarageBusinessId);
        }

        [TestMethod]
        public async Task BuildAddCustomerVehicleVmAsync_Returns_Lookups()
        {
            using var context = DbContextFactory.Create(nameof(BuildAddCustomerVehicleVmAsync_Returns_Lookups));

            context.Users.Add(new Users
            {
                Id = 100,
                GarageBusinessId = 5
            });

            context.GarageBusinessCustomer.Add(new GarageBusinessCustomer
            {
                Id = 1,
                GarageBusinessId = 5,
                GarageCustomerForename = "John",
                GarageCustomerSurname = "Murphy",
                CreatedDate = DateTime.Now,
                Active = true
            });

            context.SaveChanges();

            var lookupMock = BuildVehicleLookupMock();

            var service = new CustomerVehicleService(context, lookupMock.Object);

            var result = await service.BuildAddCustomerVehicleVmAsync(100, 5);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Data.ListofVehicleMakes.Count);
            Assert.AreEqual(2, result.Data.ListofFuelTypes.Count);
            Assert.AreEqual(2, result.Data.ListofTransmissionTypes.Count);
        }
    }
}

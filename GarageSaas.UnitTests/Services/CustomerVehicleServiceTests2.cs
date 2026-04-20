using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.UnitTests.TestHelpers;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.Tests.Services
{
    [TestClass]
    public class CustomerVehicleServiceTests
    {
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
        public async Task BuildAddCustomerVehicleVmAsync_Returns_Lookups_And_Owners()
        {
            using var context = DbContextFactory.Create();
            var lookupMock = BuildVehicleLookupMock();

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

            context.GarageBusinessCustomer.Add(new GarageBusinessCustomer
            {
                Id = 2,
                GarageBusinessId = 5,
                GarageCustomerForename = "Mary",
                GarageCustomerSurname = "Kelly",
                CreatedDate = DateTime.Now,
                Active = true
            });

            context.SaveChanges();

            var service = new CustomerVehicleService(context, lookupMock.Object);

            var result = await service.BuildAddCustomerVehicleVmAsync(100, 5);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.IsNotNull(result.Data.Vehicle);
            Assert.AreEqual(2, result.Data.GarageVehicleOwnerList.Count);
            Assert.AreEqual(2, result.Data.ListofVehicleMakes.Count);
            Assert.AreEqual(2, result.Data.ListofFuelTypes.Count);
            Assert.AreEqual(2, result.Data.ListofTransmissionTypes.Count);
        }

        [TestMethod]
        public void AddOrUpdateCustomerVehicle_Adds_Vehicle_With_Existing_Customer()
        {
            using var context = DbContextFactory.Create();
            var lookupMock = BuildVehicleLookupMock();

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
                    Id = 0,
                    VehicleRegistration = "10-D-12345",
                    VehicleMileageDate = DateTime.Now
                },
                GarageVehicleOwnerListItem = new SelectListItem
                {
                    Value = "1",
                    Text = "John Murphy"
                },
                VehicleMakeListItem = new SelectListItem { Value = "1", Text = "Ford" },
                VehicleModelListItem = new SelectListItem { Value = "10", Text = "Focus" },
                FuelTypeListItem = new SelectListItem { Value = "21", Text = "Diesel" },
                VehicleYearListItem = new SelectListItem { Value = "30", Text = "2020" },
                VehicleMileageListItem = new SelectListItem { Value = "40", Text = "50000" },
                TransmissionListItem = new SelectListItem { Value = "50", Text = "Manual" },
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
            Assert.AreEqual(10, vehicle.VehicleModelId);
            Assert.AreEqual(false, vehicle.GarageOwned);

            var ownership = context.CustomerOwnedVehicles.Single();
            Assert.AreEqual(1, ownership.GarageBusinessCustomerId);
            Assert.AreEqual(vehicle.Id, ownership.VehicleId);
        }

        [TestMethod]
        public void AddOrUpdateCustomerVehicle_Adds_GarageOwned_Vehicle()
        {
            using var context = DbContextFactory.Create();
            var lookupMock = BuildVehicleLookupMock();

            var service = new CustomerVehicleService(context, lookupMock.Object);

            var model = new VehicleAndCustomers
            {
                Vehicle = new CustomerVehicle
                {
                    Id = 0,
                    VehicleRegistration = "11-D-99999",
                    VehicleMileageDate = DateTime.Now
                },
                GarageVehicleOwnerListItem = new SelectListItem
                {
                    Value = "0",
                    Text = "Garage Owner"
                },
                VehicleMakeListItem = new SelectListItem { Value = "1", Text = "Ford" },
                VehicleModelListItem = new SelectListItem { Value = "10", Text = "Focus" },
                FuelTypeListItem = new SelectListItem { Value = "21", Text = "Diesel" },
                VehicleYearListItem = new SelectListItem { Value = "30", Text = "2020" },
                VehicleMileageListItem = new SelectListItem { Value = "40", Text = "50000" },
                TransmissionListItem = new SelectListItem { Value = "50", Text = "Manual" }
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
            using var context = DbContextFactory.Create();
            var lookupMock = BuildVehicleLookupMock();

            var service = new CustomerVehicleService(context, lookupMock.Object);

            var model = new VehicleAndCustomers
            {
                Vehicle = new CustomerVehicle
                {
                    Id = 0,
                    VehicleRegistration = "12-D-11111",
                    VehicleMileageDate = DateTime.Now
                },
                VehicleMakeListItem = new SelectListItem { Value = "1", Text = "Ford" },
                VehicleModelListItem = new SelectListItem { Value = "10", Text = "Focus" },
                FuelTypeListItem = new SelectListItem { Value = "21", Text = "Diesel" },
                VehicleYearListItem = new SelectListItem { Value = "30", Text = "2020" },
                VehicleMileageListItem = new SelectListItem { Value = "40", Text = "50000" },
                TransmissionListItem = new SelectListItem { Value = "50", Text = "Manual" },
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
            Assert.AreEqual("Kelly", customer.GarageCustomerSurname);

            var ownership = context.CustomerOwnedVehicles.Single();
            Assert.AreEqual(customer.Id, ownership.GarageBusinessCustomerId);
        }

        [TestMethod]
        public void AddOrUpdateCustomerVehicle_Updates_Existing_Vehicle()
        {
            using var context = DbContextFactory.Create();
            var lookupMock = BuildVehicleLookupMock();

            context.GarageBusinessCustomer.Add(new GarageBusinessCustomer
            {
                Id = 1,
                GarageBusinessId = 5,
                GarageCustomerForename = "John",
                GarageCustomerSurname = "Murphy",
                CreatedDate = DateTime.Now,
                Active = true
            });

            context.CustomerVehicle.Add(new CustomerVehicle
            {
                Id = 100,
                GarageBusinessId = 5,
                VehicleRegistration = "OLD-REG",
                VehicleMakeId = 1,
                VehicleModelId = 10,
                VehicleFuelTypeId = 21,
                VehicleYearId = 30,
                VehicleMileageId = 40,
                VehicleMileageDate = DateTime.Now.AddDays(-10),
                VehicleTransmissionId = 50,
                GarageOwned = false,
                CreatedDate = DateTime.Now,
                CreatedBy = "seed",
                Active = true
            });

            context.CustomerOwnedVehicles.Add(new CustomerOwnedVehicles
            {
                Id = 200,
                GarageBusinessCustomerId = 1,
                VehicleId = 100,
                GarageBusinessId = 5,
                CreatedDate = DateTime.Now,
                CreatedBy = "seed",
                Active = true
            });

            context.SaveChanges();

            var service = new CustomerVehicleService(context, lookupMock.Object);

            var model = new VehicleAndCustomers
            {
                Vehicle = new CustomerVehicle
                {
                    Id = 100,
                    VehicleRegistration = "NEW-REG",
                    VehicleMileageDate = DateTime.Now
                },
                GarageVehicleOwnerListItem = new SelectListItem
                {
                    Value = "1",
                    Text = "John Murphy"
                },
                VehicleMakeListItem = new SelectListItem { Value = "2", Text = "Toyota" },
                VehicleModelListItem = new SelectListItem { Value = "11", Text = "Corolla" },
                FuelTypeListItem = new SelectListItem { Value = "20", Text = "Petrol" },
                VehicleYearListItem = new SelectListItem { Value = "31", Text = "2021" },
                VehicleMileageListItem = new SelectListItem { Value = "41", Text = "75000" },
                TransmissionListItem = new SelectListItem { Value = "51", Text = "Automatic" },
                NCTMonthListItem = new SelectListItem { Value = "MAR", Text = "MAR" },
                NCTYearListItem = new SelectListItem { Value = "2027", Text = "2027" },
                TaxMonthListItem = new SelectListItem { Value = "APR", Text = "APR" },
                TaxYearListItem = new SelectListItem { Value = "2027", Text = "2027" }
            };

            var result = service.AddOrUpdateCustomerVehicle(model, 5, "editor");

            Assert.IsTrue(result.Success);

            var updated = context.CustomerVehicle.Single(x => x.Id == 100);
            Assert.AreEqual("NEW-REG", updated.VehicleRegistration);
            Assert.AreEqual(2, updated.VehicleMakeId);
            Assert.AreEqual(11, updated.VehicleModelId);
            Assert.AreEqual(20, updated.VehicleFuelTypeId);
            Assert.AreEqual(31, updated.VehicleYearId);
            Assert.AreEqual(41, updated.VehicleMileageId);
            Assert.AreEqual(51, updated.VehicleTransmissionId);
            Assert.AreEqual("editor", updated.UpdatedBy);
            Assert.IsNotNull(updated.UpdatedDate);
        }

        [TestMethod]
        public void AddOrUpdateCustomerVehicle_Changes_To_GarageOwned_Removes_Ownership()
        {
            using var context = DbContextFactory.Create();
            var lookupMock = BuildVehicleLookupMock();

            context.GarageBusinessCustomer.Add(new GarageBusinessCustomer
            {
                Id = 1,
                GarageBusinessId = 5,
                GarageCustomerForename = "John",
                GarageCustomerSurname = "Murphy",
                CreatedDate = DateTime.Now,
                Active = true
            });

            context.CustomerVehicle.Add(new CustomerVehicle
            {
                Id = 100,
                GarageBusinessId = 5,
                VehicleRegistration = "10-D-12345",
                VehicleMakeId = 1,
                VehicleModelId = 10,
                VehicleFuelTypeId = 21,
                VehicleYearId = 30,
                VehicleMileageId = 40,
                VehicleMileageDate = DateTime.Now,
                VehicleTransmissionId = 50,
                GarageOwned = false,
                CreatedDate = DateTime.Now,
                CreatedBy = "seed",
                Active = true
            });

            context.CustomerOwnedVehicles.Add(new CustomerOwnedVehicles
            {
                Id = 200,
                GarageBusinessCustomerId = 1,
                VehicleId = 100,
                GarageBusinessId = 5,
                CreatedDate = DateTime.Now,
                CreatedBy = "seed",
                Active = true
            });

            context.SaveChanges();

            var service = new CustomerVehicleService(context, lookupMock.Object);

            var model = new VehicleAndCustomers
            {
                Vehicle = new CustomerVehicle
                {
                    Id = 100,
                    VehicleRegistration = "10-D-12345",
                    VehicleMileageDate = DateTime.Now
                },
                GarageVehicleOwnerListItem = new SelectListItem
                {
                    Value = "0",
                    Text = "Garage Owner"
                },
                VehicleMakeListItem = new SelectListItem { Value = "1", Text = "Ford" },
                VehicleModelListItem = new SelectListItem { Value = "10", Text = "Focus" },
                FuelTypeListItem = new SelectListItem { Value = "21", Text = "Diesel" },
                VehicleYearListItem = new SelectListItem { Value = "30", Text = "2020" },
                VehicleMileageListItem = new SelectListItem { Value = "40", Text = "50000" },
                TransmissionListItem = new SelectListItem { Value = "50", Text = "Manual" }
            };

            var result = service.AddOrUpdateCustomerVehicle(model, 5, "editor");

            Assert.IsTrue(result.Success);

            var updated = context.CustomerVehicle.Single(x => x.Id == 100);
            Assert.AreEqual(true, updated.GarageOwned);
            Assert.AreEqual(0, context.CustomerOwnedVehicles.Count());
        }

        [TestMethod]
        public async Task GetCustomerVehicleForEditAsync_Returns_Preselected_Owner_And_Lookups()
        {
            using var context = DbContextFactory.Create();
            var lookupMock = BuildVehicleLookupMock();

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

            context.CustomerVehicle.Add(new CustomerVehicle
            {
                Id = 100,
                GarageBusinessId = 5,
                VehicleRegistration = "10-D-12345",
                VehicleMakeId = 1,
                VehicleModelId = 10,
                VehicleFuelTypeId = 21,
                VehicleYearId = 30,
                VehicleMileageId = 40,
                VehicleMileageDate = DateTime.Now,
                VehicleTransmissionId = 50,
                GarageOwned = false,
                CreatedDate = DateTime.Now,
                Active = true
            });

            context.CustomerOwnedVehicles.Add(new CustomerOwnedVehicles
            {
                Id = 200,
                GarageBusinessCustomerId = 1,
                VehicleId = 100,
                GarageBusinessId = 5,
                CreatedDate = DateTime.Now,
                Active = true
            });

            context.SaveChanges();

            var service = new CustomerVehicleService(context, lookupMock.Object);

            var result = await service.GetCustomerVehicleForEditAsync(100, 100, 5);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(100, result.Data.Vehicle.Id);

            var selectedOwner = result.Data.GarageVehicleOwnerList.FirstOrDefault(x => x.Selected);
            Assert.IsNotNull(selectedOwner);
            Assert.AreEqual("1", selectedOwner.Value);

            Assert.AreEqual(1, result.Data.ListofVehicleMakes.Count(x => x.Selected));
            Assert.AreEqual(1, result.Data.ListofVehicleModels.Count(x => x.Selected));
            Assert.AreEqual(1, result.Data.ListofFuelTypes.Count(x => x.Selected));
            Assert.AreEqual(1, result.Data.ListofVehicleYears.Count(x => x.Selected));
            Assert.AreEqual(1, result.Data.ListofMileages.Count(x => x.Selected));
            Assert.AreEqual(1, result.Data.ListofTransmissionTypes.Count(x => x.Selected));
        }
    }
}
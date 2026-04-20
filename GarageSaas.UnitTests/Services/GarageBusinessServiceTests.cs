using System;
using GarageSaas.Services;
using GarageSaas.UnitTests.TestHelpers;
using global::GarageSaas.Services;
using global::GarageSaas.UnitTests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services
{

        [TestClass]
        public class GarageBusinessServiceTests
        {
            [TestMethod]
            public void GetGarageBusinessDetail_Returns_Fail_When_User_Not_Found()
            {
                using var context = DbContextFactory.Create(nameof(GetGarageBusinessDetail_Returns_Fail_When_User_Not_Found));

                var service = new GarageBusinessService(context);

                var result = service.GetGarageBusinessDetail(1, 999);

                Assert.IsFalse(result.Success);
                Assert.AreEqual("User not found.", result.ErrorMessage);
            }

            [TestMethod]
            public void GetGarageBusinessDetail_Returns_Garage_When_User_Belongs_To_Garage()
            {
                using var context = DbContextFactory.Create(nameof(GetGarageBusinessDetail_Returns_Garage_When_User_Belongs_To_Garage));

                context.GarageBusiness.Add(new GarageBusiness
                {
                    Id = 1,
                    GarageBusinessName = "Test Garage"
                });

                context.Users.Add(new Users
                {
                    Id = 10,
                    GarageBusinessId = 1
                });

                context.SaveChanges();

                var service = new GarageBusinessService(context);

                var result = service.GetGarageBusinessDetail(1, 10);

                Assert.IsTrue(result.Success);
                Assert.IsNotNull(result.Data);
                Assert.AreEqual("Test Garage", result.Data.GarageBusinessName);
            }

            [TestMethod]
            public void UpdateGarageBusiness_Updates_Fields()
            {
                using var context = DbContextFactory.Create(nameof(UpdateGarageBusiness_Updates_Fields));

                context.GarageBusiness.Add(new GarageBusiness
                {
                    Id = 1,
                    GarageBusinessName = "Old Name",
                    GarageAddressLine1 = "Old Address",
                    GarageEmailAddress = "old@test.com"
                });

                context.Users.Add(new Users
                {
                    Id = 10,
                    GarageBusinessId = 1
                });

                context.SaveChanges();

                var service = new GarageBusinessService(context);

                var updateModel = new GarageBusiness
                {
                    Id = 1,
                    GarageBusinessName = "New Name",
                    GarageAddressLine1 = "New Address",
                    GarageEmailAddress = "new@test.com",
                    GaragePhoneNumber = "12345",
                    GarageMobileNumber = "99999"
                };

                var result = service.UpdateGarageBusiness(updateModel, 1, 10, "tester");

                Assert.IsTrue(result.Success);
                Assert.IsNotNull(result.Data);
                Assert.AreEqual("New Name", result.Data.GarageBusinessName);
                Assert.AreEqual("New Address", result.Data.GarageAddressLine1);
                Assert.AreEqual("new@test.com", result.Data.GarageEmailAddress);
                Assert.AreEqual("tester", result.Data.UpdatedBy);
                Assert.IsNotNull(result.Data.UpdatedDate);
            }
        }
    }
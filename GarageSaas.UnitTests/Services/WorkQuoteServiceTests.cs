using System;
using System.Linq;
using GarageSaas.Services;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.Tests.Services
{
    [TestClass]
    public class WorkQuoteServiceTests
    {
        [TestMethod]
        public void GetWorkQuote_Returns_Fail_When_Not_Found()
        {
            using var context = DbContextFactory.Create();

            var service = new WorkQuoteService(context);

            var result = service.GetWorkQuote(999, 5);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("WorkQuote not found.", result.ErrorMessage);
        }

        [TestMethod]
        public void GetWorkQuote_Returns_Combined_Model_When_Found()
        {
            using var context = DbContextFactory.Create();

            context.WorkQuote.Add(new WorkQuote
            {
                Id = 1,
                GarageBusinessCustomerId = 5,
                VehicleId = 100,
                CustomerId = 10,
                WorkQuoteDate = new DateTime(2026, 1, 1),
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.WorkItem.Add(new WorkItem
            {
                Id = 200,
                GarageBusinessCustomerId = 5,
                VehicleId = 100,
                CustomerId = 10,
                RepairInstructions = "Replace clutch",
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem
            {
                Id = 300,
                GarageBusinessCustomerId = 5,
                WorkQuoteId = 1,
                WorkItemId = 200,
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.SaveChanges();

            var service = new WorkQuoteService(context);

            var result = service.GetWorkQuote(1, 5);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(300, result.Data.Id);
            Assert.AreEqual(1, result.Data.WorkQuoteId);
            Assert.AreEqual(200, result.Data.WorkItemId);
            Assert.AreEqual(100, result.Data.VehicleId);
            Assert.AreEqual(10, result.Data.CustomerId);
            Assert.AreEqual(new DateTime(2026, 1, 1), result.Data.WorkQuoteDate);
            Assert.AreEqual(1, result.Data.WorkItems.Count);
            Assert.AreEqual("Replace clutch", result.Data.WorkItems[0].RepairInstructions);
        }

        [TestMethod]
        public void GetWorkQuote_Returns_Fail_When_GarageBusiness_Does_Not_Match()
        {
            using var context = DbContextFactory.Create();

            context.WorkQuote.Add(new WorkQuote
            {
                Id = 1,
                GarageBusinessCustomerId = 5,
                VehicleId = 100,
                CustomerId = 10,
                WorkQuoteDate = new DateTime(2026, 1, 1),
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.SaveChanges();

            var service = new WorkQuoteService(context);

            var result = service.GetWorkQuote(1, 99);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("WorkQuote not found.", result.ErrorMessage);
        }

        [TestMethod]
        public void GetWorkQuotesForVehicle_Returns_Only_Quotes_For_Vehicle_And_Garage()
        {
            using var context = DbContextFactory.Create();

            context.WorkQuote.Add(new WorkQuote
            {
                Id = 1,
                GarageBusinessCustomerId = 5,
                VehicleId = 100,
                CustomerId = 10,
                WorkQuoteDate = new DateTime(2026, 1, 1),
                CreatedDate = DateTime.Now
            });

            context.WorkQuote.Add(new WorkQuote
            {
                Id = 2,
                GarageBusinessCustomerId = 5,
                VehicleId = 200,
                CustomerId = 10,
                WorkQuoteDate = new DateTime(2026, 2, 1),
                CreatedDate = DateTime.Now
            });

            context.WorkQuote.Add(new WorkQuote
            {
                Id = 3,
                GarageBusinessCustomerId = 99,
                VehicleId = 100,
                CustomerId = 10,
                WorkQuoteDate = new DateTime(2026, 3, 1),
                CreatedDate = DateTime.Now
            });

            context.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem
            {
                Id = 10,
                GarageBusinessCustomerId = 5,
                WorkQuoteId = 1,
                WorkItemId = 1000,
                CreatedDate = DateTime.Now
            });

            context.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem
            {
                Id = 11,
                GarageBusinessCustomerId = 5,
                WorkQuoteId = 2,
                WorkItemId = 2000,
                CreatedDate = DateTime.Now
            });

            context.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem
            {
                Id = 12,
                GarageBusinessCustomerId = 99,
                WorkQuoteId = 3,
                WorkItemId = 3000,
                CreatedDate = DateTime.Now
            });

            context.SaveChanges();

            var service = new WorkQuoteService(context);

            var result = service.GetWorkQuotesForVehicle(100, 5);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Data.Count);
            Assert.AreEqual(1, result.Data[0].WorkQuoteId);
            Assert.AreEqual(100, result.Data[0].VehicleId);
        }

        [TestMethod]
        public void GetWorkQuotesForWorkItem_Returns_Only_Quotes_For_WorkItem_And_Garage()
        {
            using var context = DbContextFactory.Create();

            context.WorkQuote.Add(new WorkQuote
            {
                Id = 1,
                GarageBusinessCustomerId = 5,
                VehicleId = 100,
                CustomerId = 10,
                WorkQuoteDate = new DateTime(2026, 1, 1),
                CreatedDate = DateTime.Now
            });

            context.WorkQuote.Add(new WorkQuote
            {
                Id = 2,
                GarageBusinessCustomerId = 5,
                VehicleId = 100,
                CustomerId = 10,
                WorkQuoteDate = new DateTime(2026, 2, 1),
                CreatedDate = DateTime.Now
            });

            context.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem
            {
                Id = 10,
                GarageBusinessCustomerId = 5,
                WorkQuoteId = 1,
                WorkItemId = 1000,
                CreatedDate = DateTime.Now
            });

            context.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem
            {
                Id = 11,
                GarageBusinessCustomerId = 5,
                WorkQuoteId = 2,
                WorkItemId = 2000,
                CreatedDate = DateTime.Now
            });

            context.SaveChanges();

            var service = new WorkQuoteService(context);

            var result = service.GetWorkQuotesForWorkItem(1000, 5);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Data.Count);
            Assert.AreEqual(1, result.Data[0].WorkQuoteId);
            Assert.AreEqual(1000, result.Data[0].WorkItemId);
        }

        [TestMethod]
        public void AddOrUpdateWorkQuote_Returns_Fail_When_Model_Is_Null()
        {
            using var context = DbContextFactory.Create();

            var service = new WorkQuoteService(context);

            var result = service.AddOrUpdateWorkQuote(null, 5, "tester");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("WorkQuote model is null.", result.ErrorMessage);
        }

        [TestMethod]
        public void AddOrUpdateWorkQuote_Returns_Fail_When_WorkItemId_Is_Missing()
        {
            using var context = DbContextFactory.Create();

            var service = new WorkQuoteService(context);

            var model = new CombinedWorkQuoteWorkitem
            {
                VehicleId = 100,
                CustomerId = 10,
                WorkQuoteDate = DateTime.Now
            };

            var result = service.AddOrUpdateWorkQuote(model, 5, "tester");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("WorkItemId is required.", result.ErrorMessage);
        }

        [TestMethod]
        public void AddOrUpdateWorkQuote_Returns_Fail_When_VehicleId_Is_Missing()
        {
            using var context = DbContextFactory.Create();

            var service = new WorkQuoteService(context);

            var model = new CombinedWorkQuoteWorkitem
            {
                WorkItemId = 1000,
                CustomerId = 10,
                WorkQuoteDate = DateTime.Now
            };

            var result = service.AddOrUpdateWorkQuote(model, 5, "tester");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("VehicleId is required.", result.ErrorMessage);
        }

        [TestMethod]
        public void AddOrUpdateWorkQuote_Adds_New_WorkQuote_And_Link()
        {
            using var context = DbContextFactory.Create();

            var service = new WorkQuoteService(context);

            var model = new CombinedWorkQuoteWorkitem
            {
                WorkItemId = 1000,
                VehicleId = 100,
                CustomerId = 10,
                WorkQuoteDate = new DateTime(2026, 1, 1)
            };

            var result = service.AddOrUpdateWorkQuote(model, 5, "tester");

            Assert.IsTrue(result.Success);

            Assert.AreEqual(1, context.WorkQuote.Count());
            Assert.AreEqual(1, context.WorkQuoteWorkItem.Count());

            var savedQuote = context.WorkQuote.Single();
            var savedLink = context.WorkQuoteWorkItem.Single();

            Assert.AreEqual(5, savedQuote.GarageBusinessCustomerId);
            Assert.AreEqual(100, savedQuote.VehicleId);
            Assert.AreEqual(10, savedQuote.CustomerId);
            Assert.AreEqual(new DateTime(2026, 1, 1), savedQuote.WorkQuoteDate);
            Assert.AreEqual("tester", savedQuote.CreatedBy);

            Assert.AreEqual(5, savedLink.GarageBusinessCustomerId);
            Assert.AreEqual(savedQuote.Id, savedLink.WorkQuoteId);
            Assert.AreEqual(1000, savedLink.WorkItemId);
            Assert.AreEqual("tester", savedLink.CreatedBy);

            Assert.AreEqual(savedQuote.Id, result.Data.WorkQuoteId);
            Assert.AreEqual(savedLink.Id, result.Data.Id);
            Assert.AreEqual(5, result.Data.GarageBusinessCustomerId);
        }

        [TestMethod]
        public void AddOrUpdateWorkQuote_Updates_Existing_WorkQuote_And_Link()
        {
            using var context = DbContextFactory.Create();

            context.WorkQuote.Add(new WorkQuote
            {
                Id = 1,
                GarageBusinessCustomerId = 5,
                VehicleId = 100,
                CustomerId = 10,
                WorkQuoteDate = new DateTime(2026, 1, 1),
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem
            {
                Id = 20,
                GarageBusinessCustomerId = 5,
                WorkQuoteId = 1,
                WorkItemId = 1000,
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.SaveChanges();

            var service = new WorkQuoteService(context);

            var model = new CombinedWorkQuoteWorkitem
            {
                WorkQuoteId = 1,
                WorkItemId = 2000,
                VehicleId = 200,
                CustomerId = 20,
                WorkQuoteDate = new DateTime(2026, 2, 1)
            };

            var result = service.AddOrUpdateWorkQuote(model, 5, "editor");

            Assert.IsTrue(result.Success);

            var updatedQuote = context.WorkQuote.Single(x => x.Id == 1);
            var updatedLink = context.WorkQuoteWorkItem.Single(x => x.WorkQuoteId == 1);

            Assert.AreEqual(200, updatedQuote.VehicleId);
            Assert.AreEqual(20, updatedQuote.CustomerId);
            Assert.AreEqual(new DateTime(2026, 2, 1), updatedQuote.WorkQuoteDate);
            Assert.AreEqual("editor", updatedQuote.UpdatedBy);
            Assert.IsNotNull(updatedQuote.UpdatedDate);

            Assert.AreEqual(2000, updatedLink.WorkItemId);
            Assert.AreEqual("editor", updatedLink.UpdatedBy);
            Assert.IsNotNull(updatedLink.UpdatedDate);
        }

        [TestMethod]
        public void AddOrUpdateWorkQuote_Returns_Fail_When_Update_Target_Not_Found()
        {
            using var context = DbContextFactory.Create();

            var service = new WorkQuoteService(context);

            var model = new CombinedWorkQuoteWorkitem
            {
                WorkQuoteId = 999,
                WorkItemId = 1000,
                VehicleId = 100,
                CustomerId = 10,
                WorkQuoteDate = DateTime.Now
            };

            var result = service.AddOrUpdateWorkQuote(model, 5, "editor");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("WorkQuote not found.", result.ErrorMessage);
        }

        [TestMethod]
        public void DeleteWorkQuote_Removes_Quote_And_Links()
        {
            using var context = DbContextFactory.Create();

            context.WorkQuote.Add(new WorkQuote
            {
                Id = 1,
                GarageBusinessCustomerId = 5,
                VehicleId = 100,
                CustomerId = 10,
                WorkQuoteDate = DateTime.Now,
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.WorkQuoteWorkItem.Add(new WorkQuoteWorkItem
            {
                Id = 20,
                GarageBusinessCustomerId = 5,
                WorkQuoteId = 1,
                WorkItemId = 1000,
                CreatedDate = DateTime.Now,
                CreatedBy = "seed"
            });

            context.SaveChanges();

            var service = new WorkQuoteService(context);

            var result = service.DeleteWorkQuote(1, 5);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, context.WorkQuote.Count());
            Assert.AreEqual(0, context.WorkQuoteWorkItem.Count());
        }

        [TestMethod]
        public void DeleteWorkQuote_Returns_Fail_When_Not_Found()
        {
            using var context = DbContextFactory.Create();

            var service = new WorkQuoteService(context);

            var result = service.DeleteWorkQuote(999, 5);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("WorkQuote not found.", result.ErrorMessage);
        }
    }
}
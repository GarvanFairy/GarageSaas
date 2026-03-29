using GarageSaas.Tests.TestHelpers;
using SignupAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarageSaas.Tests.Services
{
    internal class GarageBusinessServiceTests
    {

        [TestMethod]
        public void Can_Create_InMemory_Context()
        {
            using var context = DbContextFactory.Create("test");

            context.GarageBusinessCustomer.Add(new GarageBusinessCustomer
            {
                Id = 1,
                GarageBusinessId = 10,
                GarageCustomerForename = "Test",
                CreatedDate = DateTime.Now,
                Active = true
            });

            context.SaveChanges();

            var count = context.GarageBusinessCustomer.Count();

            Assert.AreEqual(1, count);
        }

    }
}



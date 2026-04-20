using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;
using System;

namespace GarageSaas.UnitTests.TestHelpers
{
    public static class DbContextFactory
    {
        public static SignupContext Create(string databaseName = null)
        {
            var options = new DbContextOptionsBuilder<SignupContext>()
                .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
                .Options;

            return new SignupContext(options);
        }
    }
}
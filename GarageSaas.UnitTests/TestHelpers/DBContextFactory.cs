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
                // Functional transactions only; SQL fixtures establish real rollback.
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new SignupContext(options);
        }
    }
}

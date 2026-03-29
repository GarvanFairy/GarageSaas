using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;

namespace GarageSaas.Tests.TestHelpers
{
    public static class DbContextFactory
    {
        public static SignupContext Create(string dbName)
        {
            var options = new DbContextOptionsBuilder<SignupContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new SignupContext(options);
        }
    }
}

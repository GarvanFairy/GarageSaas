using System;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SignupAPI.Configuration
{
    public static class ProductionDatabaseConfiguration
    {
        public static void Validate(IConfiguration configuration)
        {
            const string key = "ConnectionStrings:GarageSaasDBConnection";
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Missing production configuration: " + key);
            SqlConnectionStringBuilder sql;
            try { sql = new SqlConnectionStringBuilder(value); }
            catch (ArgumentException) { throw new InvalidOperationException("Invalid production configuration: " + key); }
            if (string.IsNullOrWhiteSpace(sql.DataSource) || string.IsNullOrWhiteSpace(sql.InitialCatalog) ||
                sql.Encrypt == SqlConnectionEncryptOption.Optional || sql.TrustServerCertificate || sql.PersistSecurityInfo)
                throw new InvalidOperationException("Production SQL configuration requires server, database, encryption, certificate validation and Persist Security Info=false.");
        }
    }
}

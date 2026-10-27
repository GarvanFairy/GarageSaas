using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;
// Offline only: no connection is opened and no startup/configuration/secrets are loaded.
using var db = new SignupContext(new DbContextOptionsBuilder<SignupContext>()
    .UseSqlServer("Server=offline.invalid;Database=OfflineModelOnly;Encrypt=True;TrustServerCertificate=False").Options);
var sql = db.Database.GenerateCreateScript();
if (sql.Contains("__EFMigrationsHistory") || sql.Contains("CREATE DATABASE", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Unexpected migration/database operation.");
var guard = "SET NOCOUNT ON;\nSET XACT_ABORT ON;\nIF DB_NAME() <> N'GarageSaas_Staging' THROW 51000, 'Only GarageSaas_Staging is allowed.', 1;\nIF CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) NOT IN (N'garagesaasdbserver', N'garagesaasdbserver.database.windows.net') THROW 51000, 'Wrong SQL server.', 1;\nIF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped=0) THROW 51000, 'Database must be empty.', 1;\nBEGIN TRANSACTION;\n";
var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../01-schema.sql"));
File.WriteAllText(output, guard + sql + "\nCOMMIT TRANSACTION;\n");
Console.WriteLine($"Generated offline: {output}; {db.Model.GetEntityTypes().Count()} entity types. No database access.");
SeedFile.Write(db, Path.Combine(Path.GetDirectoryName(output)!, "02-synthetic-seed.sql"));
// Local-only preparation helpers. These scripts are not executed by this tool.
var tables = db.Model.GetEntityTypes().Select(e => e.GetTableName()!).Distinct().OrderBy(x=>x).ToArray();
var lookups = new[]{"FuelType","TransmissionType","VehicleMake","VehicleModel","VehicleYears","Mileage","GarageVehicleOwner"};
var targetGuard = "SET XACT_ABORT ON;\nIF DB_NAME() <> N'GarageSaas_Staging' THROW 51000, 'Wrong database.', 1;\nIF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) NOT IN (N'garagesaasdbserver',N'garagesaasdbserver.database.windows.net') THROW 51000, 'Wrong server.', 1;\n";
var permissions = targetGuard + "BEGIN TRANSACTION;\nCREATE USER [garagesaas-stg-d49b2f61-weu] FROM EXTERNAL PROVIDER;\nCREATE ROLE [GarageSaasStagingRuntime] AUTHORIZATION [dbo];\n";
foreach(var table in tables.Where(t => t != "ServiceHistory" && t != "GarageOwnedVehicles")) permissions += $"GRANT {(lookups.Contains(table) ? "SELECT" : "SELECT, INSERT, UPDATE, DELETE")} ON OBJECT::[dbo].[{table}] TO [GarageSaasStagingRuntime];\n";
permissions += "ALTER ROLE [GarageSaasStagingRuntime] ADD MEMBER [garagesaas-stg-d49b2f61-weu];\nCOMMIT TRANSACTION;\n";
File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"03-runtime-access.sql"),permissions);
var verify = targetGuard + "IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) <> 23 THROW 51000, 'Unexpected schema table count.', 1;\nIF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL THROW 51000, 'Unexpected migration history.', 1;\nDBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;\n";
foreach(var table in tables) verify += $"SELECT N'{table}' AS TableName, COUNT_BIG(*) AS [RowCount] FROM [dbo].[{table}];\n";
verify += "IF EXISTS(SELECT 1 FROM dbo.VehicleInvoice WHERE SubTotal <> 100 OR Vat <> 23.50 OR Total <> 123.50) THROW 51000, 'Unexpected fixture totals.', 1;\nSELECT InvoiceStatus, Paid, COUNT(*) AS [Records] FROM dbo.VehicleInvoice GROUP BY InvoiceStatus,Paid;\nSELECT r.name AS RoleName,p.permission_name,p.state_desc,OBJECT_NAME(p.major_id) AS TableName FROM sys.database_permissions p JOIN sys.database_principals r ON p.grantee_principal_id=r.principal_id WHERE r.name=N'GarageSaasStagingRuntime';\n";
File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"04-verify.sql"),verify);




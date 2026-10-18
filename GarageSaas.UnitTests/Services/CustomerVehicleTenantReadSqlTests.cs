using GarageSaas.UnitTests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlCustomerVehicleReads")]
public class CustomerVehicleTenantReadSqlTests
{
    private DisposableSqlDatabase? _database;
    private static bool _unsafeCleanup;
    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        if (_unsafeCleanup) Assert.Inconclusive("Earlier database cleanup was uncertain; no further database creation.");
        _database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_CUSTOMER_VEHICLE_READ_SQL_TESTS", "QuoteCoordination");
        _database.Create();
        TestContext.WriteLine($"Owned disposable database: {_database.DatabaseName}");
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_database == null) return;
        try
        {
            await _database.DeleteAsync();
            TestContext.WriteLine($"Guarded deletion completed: {_database.DatabaseName}");
        }
        catch { _unsafeCleanup = true; throw; }
    }

    [DataTestMethod]
    [DataRow("foreign-roots")]
    [DataRow("foreign-vehicle")]
    [DataRow("foreign-customer")]
    [DataRow("foreign-label")]
    [DataRow("duplicate")]
    [DataRow("dangling-vehicle")]
    [DataRow("dangling-customer")]
    [DataRow("valid-owner")]
    [DataRow("valid-after-foreign-link")]
    [DataRow("ambiguous-owner")]
    [DataRow("garage-owned")]
    [DataRow("missing-lookups")]
    [DataRow("invalid-garage")]
    public async Task RelationalReadsValidateAllOwnershipEndpointsWithoutWrites(string scenario)
    {
        using var db = new SignupContext(_database!.Options());
        await CustomerVehicleReadCases.Check(db, scenario);
    }
}

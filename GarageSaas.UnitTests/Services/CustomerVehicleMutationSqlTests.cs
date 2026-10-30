using GarageSaas.UnitTests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace GarageSaas.UnitTests.Services;

[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlCustomerVehicleMutations")]
public class CustomerVehicleMutationSqlTests
{
    private DisposableSqlDatabase? _database;
    private static bool _unsafeCleanup;
    public TestContext TestContext { get; set; } = null!;
    [TestInitialize]
    public void Initialize()
    {
        if (_unsafeCleanup) Assert.Inconclusive("Uncertain earlier cleanup: no more database creation.");
        _database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_CUSTOMER_VEHICLE_MUTATION_SQL_TESTS", "QuoteCoordination");
        _database.RegisterCustomerDiagnostics();
        _database.Create(); TestContext.WriteLine($"Owned disposable database: {_database.DatabaseName}");
    }
    [TestCleanup]
    public async Task Cleanup()
    {
        if (_database == null) return;
        try { await _database.DeleteAsync(); TestContext.WriteLine($"Guarded deletion completed: {_database.DatabaseName}"); }
        catch { _unsafeCleanup = true; throw; }
    }
    [DataTestMethod]
    [DataRow("inline-create", 0, false)] [DataRow("existing-create", 0, false)]
    [DataRow("garage-create", 0, false)] [DataRow("inline-update", 0, false)]
    [DataRow("existing-update", 0, false)] [DataRow("customer-create", 0, false)]
    [DataRow("customer-update", 0, false)] [DataRow("combined-create", 0, false)]
    [DataRow("foreign-vehicle", 0, false)] [DataRow("missing-vehicle", 0, false)]
    [DataRow("foreign-owner", 0, false)] [DataRow("foreign-customer", 0, false)]
    [DataRow("missing-owner", 0, false)] [DataRow("negative-vehicle", 0, false)]
    [DataRow("missing-customer", 0, false)] [DataRow("combined-existing-vehicle", 0, false)]
    [DataRow("inline-create", 1, false)] [DataRow("inline-create", 1, true)]
    [DataRow("inline-create", 2, false)] [DataRow("inline-create", 2, true)]
    [DataRow("inline-create", 3, false)] [DataRow("inline-create", 3, true)]
    [DataRow("combined-create", 1, false)] [DataRow("combined-create", 1, true)]
    [DataRow("combined-create", 2, false)] [DataRow("combined-create", 2, true)]
    [DataRow("combined-create", 3, false)] [DataRow("combined-create", 3, true)]
    [DataRow("inline-update", 2, true)] [DataRow("existing-update", 1, true)]
    [DataRow("customer-create", 1, true)] [DataRow("customer-update", 1, true)]
    [DataRow("garage-update", 0, false)] [DataRow("garage-update", 1, true)]
    [DataRow("pending", 0, false)] [DataRow("commit-uncertainty", 0, false)]
    public void AtomicMutationsAndRollback(string operation, int failAt, bool after)
        => CustomerVehicleMutationCases.Check(_database!.Options(), operation, failAt, after, true, _database.HelperEvidence);
}

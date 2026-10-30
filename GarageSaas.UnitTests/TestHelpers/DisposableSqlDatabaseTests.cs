using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GarageSaas.UnitTests.TestHelpers;

// Pure safeguard checks: no utility invocation, connections or database operations.
[TestClass]
public class DisposableSqlDatabaseTests
{
    [DataTestMethod]
    [DataRow(null, false)] [DataRow("", false)] [DataRow("0", false)] [DataRow("true", false)] [DataRow("1", true)]
    public void OnlyExactExplicitOptInEnablesFixture(string? value, bool expected) =>
        Assert.AreEqual(expected, DisposableSqlDatabase.Enabled("fixture", _ => value));

    [DataTestMethod]
    [DataRow(null)] [DataRow("")] [DataRow("localhost")] [DataRow("production")]
    [DataRow(@"(localdb)\MSSQLLocalDB")] [DataRow(@"Server=(localdb)\GarageSaasIntegrationTests;Database=App")]
    public void ArbitraryServersAndConnectionStringsAreRejected(string? server) =>
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateServer(server));

    [TestMethod]
    public void DedicatedInstanceIsAllowed() => DisposableSqlDatabase.ValidateServer(DisposableSqlDatabase.Server);

    [TestMethod]
    public void SharedServerNeedsItsOwnExactOptIn()
    {
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateServer("GARVAN_ASUS"));
        DisposableSqlDatabase.ValidateServer("GARVAN_ASUS", sharedServerOptIn: true);
        DisposableSqlDatabase.ValidateServer(DisposableSqlDatabase.Server, sharedServerOptIn: true);
    }

    [DataTestMethod]
    [DataRow("localhost")] [DataRow("lpc:GARVAN_ASUS")] [DataRow("tcp:GARVAN_ASUS")]
    [DataRow(@"GARVAN_ASUS\Other")] [DataRow("Server=GARVAN_ASUS;Database=Garagesaas")]
    public void SharedOptInNeverAllowsAliasesProtocolsOrConnectionStrings(string target) =>
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateServer(target, sharedServerOptIn: true));

    private static SqlServerIdentity ApprovedIdentity() => new("Garvan_Asus", "GARVAN_ASUS", null,
        "Garvan_Asus", "Garvan_Asus", false, false, "Shared memory");

    [TestMethod]
    public void ApprovedSharedMemoryDefaultInstancePassesIdentityGuard() =>
        DisposableSqlDatabase.ValidateSharedIdentity(ApprovedIdentity(), "GARVAN_ASUS");

    [DataTestMethod]
    [DataRow("machine")] [DataRow("physical")] [DataRow("instance")] [DataRow("server")]
    [DataRow("registered")] [DataRow("localdb")] [DataRow("clustered")] [DataRow("transport")] [DataRow("client-machine")]
    public void SharedServerOptInCannotBypassResolvedIdentity(string kind)
    {
        var identity = ApprovedIdentity();
        identity = kind switch
        {
            "machine" => identity with { Machine = "remote" },
            "physical" => identity with { PhysicalMachine = "remote" },
            "instance" => identity with { Instance = "Other" },
            "server" => identity with { Server = "alias" },
            "registered" => identity with { RegisteredServer = "stale-name" },
            "localdb" => identity with { IsLocalDb = true },
            "clustered" => identity with { IsClustered = true },
            "transport" => identity with { Transport = "TCP" },
            _ => identity
        };
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateSharedIdentity(identity,
            kind == "client-machine" ? "another-machine" : "GARVAN_ASUS"));
    }

    [DataTestMethod]
    [DataRow("Garagesaas")] [DataRow("GARAGESAAS")] [DataRow("master")]
    [DataRow("tempdb")] [DataRow("other-application")]
    [DataRow("GarageSaas_InvoiceAtomic_Test_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n")]
    public void NonTestCatalogsCannotBeCreatedOrDeleted(string catalog) =>
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateCatalog(catalog));

    [TestMethod]
    public void OnlyMasterIsAllowedForMetadataAndGeneratedNameForTestData()
    {
        DisposableSqlDatabase.ValidateCatalog("master", allowMaster: true);
        DisposableSqlDatabase.ValidateCatalog("GarageSaas_InvoiceAtomic_Test_" + new string('d', 32));
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateCatalog("Garagesaas", allowMaster: true));
        DisposableSqlDatabase.ValidateAutoClose(false);
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateAutoClose(true));
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateAutoClose(null));
    }

    [DataTestMethod]
    [DataRow(false, 0, 0, 0)] [DataRow(true, 1, 0, 0)] [DataRow(true, 0, 1, 0)] [DataRow(true, 0, 0, 1)]
    public void SharedDdlSideEffectsOrInvisibleMetadataPreventDatabaseOperations(bool visible, int triggers, int notifications, int modelObjects) =>
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateSharedMetadata(visible, triggers, notifications, modelObjects));

    [TestMethod]
    public void VisibleSharedMetadataWithoutDdlHooksPassesGuard() =>
        DisposableSqlDatabase.ValidateSharedMetadata(true, 0, 0, 0);

    [TestMethod]
    public void ResolvedLocalPipeAndServerIdentityMustAgree() =>
        DisposableSqlDatabase.ValidateIdentity(@"np:\\.\pipe\LOCALDB#ABC123\tsql\query", Environment.MachineName, "LOCALDB#ABC123", true);

    [DataTestMethod]
    [DataRow("machine")] [DataRow("instance")] [DataRow("remote-pipe")] [DataRow("not-localdb")] [DataRow("alias")]
    public void MismatchedResolvedIdentityIsRejected(string kind) =>
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateIdentity(
            kind == "remote-pipe" ? @"np:\\remote\pipe\LOCALDB#ABC123\tsql\query" : kind == "alias" ? "alias" : @"np:\\.\pipe\LOCALDB#ABC123\tsql\query",
            kind == "machine" ? "unapproved-machine" : Environment.MachineName,
            kind == "instance" ? "LOCALDB#OTHER" : "LOCALDB#ABC123", kind != "not-localdb"));

    [DataTestMethod]
    [DataRow("not-created")] [DataRow("not-marked")] [DataRow("wrong-name")] [DataRow("wrong-owner")]
    [DataRow("missing-owner")] [DataRow("application-name")]
    public void UnverifiedDatabaseCannotBeDeleted(string kind)
    {
        var name = kind == "application-name" ? "GarageSaas" : "GarageSaas_InvoiceAtomic_Test_" + new string('a', 32);
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateOwnership(
            kind != "not-created", kind != "not-marked", name, kind == "wrong-name" ? name + "other" : name,
            "owner", kind == "wrong-owner" ? "other" : kind == "missing-owner" ? null : "owner"));
    }

    [TestMethod]
    public void ExactOwnedDatabasePassesGuard()
    {
        var name = "GarageSaas_QuoteConversion_Test_" + new string('b', 32);
        DisposableSqlDatabase.ValidateOwnership(true, true, name, name, "owner", "owner");
    }

    [DataTestMethod]
    [DataRow("QuoteAtomic")]
    [DataRow("QuoteCoordination")]
    public void QuotePrefixesStillRequireExactCreationAndOwnership(string category)
    {
        var name = "GarageSaas_" + category + "_Test_" + new string('e', 32);
        DisposableSqlDatabase.ValidateCatalog(name);
        DisposableSqlDatabase.ValidateOwnership(true, true, name, name, "owner", "owner");
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateOwnership(false, true, name, name, "owner", "owner"));
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateOwnership(true, true, name, name, "owner", "other-run"));
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateOwnership(true, true, name, "Garagesaas", "owner", "owner"));
    }

    [TestMethod]
    public async Task DrainWaitsForWorkersWithoutRequiringSql()
    {
        // Exercise the same registry used by destructive cleanup, without SQL.
        var worker = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new SqlTestWorkers(); _ = registry.Track(worker.Task);
        var drained = registry.DrainAsync(TimeSpan.FromSeconds(2)); Assert.IsFalse(drained.IsCompleted);
        worker.SetResult(true); await drained;
    }

    [TestMethod]
    public async Task TimedOutWorkerRemainsRegisteredAndMustFinishBeforeDrainSucceeds()
    {
        var worker = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new SqlTestWorkers(); _ = registry.Track(worker.Task);
        await Assert.ThrowsExceptionAsync<TimeoutException>(() => registry.DrainAsync(TimeSpan.FromMilliseconds(10)));
        Assert.IsFalse(worker.Task.IsCompleted);
        var drained = registry.DrainAsync(TimeSpan.FromSeconds(2)); Assert.IsFalse(drained.IsCompleted);
        worker.SetResult(true); await drained;
    }

    [TestMethod]
    public void InstanceWithApplicationDatabaseIsRejected() =>
        Assert.ThrowsException<InvalidOperationException>(() => DisposableSqlDatabase.ValidateDatabaseInventory(new[] { "GarageSaas" }));

    [TestMethod]
    public void InstanceWithOnlyTestDatabasesPassesInventoryGuard() =>
        DisposableSqlDatabase.ValidateDatabaseInventory(new[] { "GarageSaas_Registration_Test_" + new string('c', 32) });
}

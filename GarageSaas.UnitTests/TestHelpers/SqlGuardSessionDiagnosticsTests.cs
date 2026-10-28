using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;

namespace GarageSaas.UnitTests.TestHelpers;

[TestClass]
public class SqlGuardSessionDiagnosticsTests
{
    private static DbDataReader Observation(int count, int rows, Guid? connection, string status = "sleeping")
    {
        var totals = new DataTable(); totals.Columns.Add("count", typeof(int)); totals.Rows.Add(count);
        var sessions = new DataTable();
        sessions.Columns.Add("user", typeof(bool)); sessions.Columns.Add("status", typeof(string));
        sessions.Columns.Add("request", typeof(bool)); sessions.Columns.Add("transactions", typeof(int));
        sessions.Columns.Add("connection", typeof(Guid));
        for (var i = 0; i < rows; i++) sessions.Rows.Add(true, status, false, 0, connection.HasValue ? connection.Value : DBNull.Value);
        return new DataTableReader(new[] { totals, sessions });
    }
    private static JsonElement Details(SqlCleanupDiagnostics diagnostics, List<string> output)
    {
        diagnostics.Record("ActiveSessionGuard", new InvalidOperationException("synthetic"), true, true,
            false, true, 1, "not-emitted", new SqlTestWorkers(), () => throw new AssertFailedException("No SQL allowed"));
        using var document = JsonDocument.Parse(output.Last()["SQL-CLEANUP-DIAGNOSTIC:".Length..]);
        return document.RootElement.GetProperty("guardSessionDetails").Clone();
    }

    [TestMethod]
    public void MaterializedCountIsNotCappedAndPrivateIdsAreNotEmitted()
    {
        var output = new List<string>(); var diagnostics = new SqlCleanupDiagnostics(_ => "1", output.Add);
        var id = Guid.NewGuid(); diagnostics.TrackConnection(id, SqlFixtureConnectionRole.OwnershipVerification);
        Assert.AreEqual(23, diagnostics.InspectGuard(() => Observation(23, 16, id)));
        var detail = Details(diagnostics, output);
        Assert.AreEqual("available", detail.GetProperty("availability").GetString());
        Assert.IsTrue(detail.GetProperty("truncated").GetBoolean());
        Assert.AreEqual(16, detail.GetProperty("sessions").GetArrayLength());
        Assert.AreEqual("OwnershipVerification", detail.GetProperty("sessions")[0].GetProperty("fixtureRole").GetString());
        Assert.IsFalse(output.Last().Contains(id.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(0, diagnostics.InspectGuard(() => Observation(0, 0, null)));
        detail = Details(diagnostics, output);
        Assert.AreEqual("available", detail.GetProperty("availability").GetString());
        Assert.IsFalse(detail.GetProperty("truncated").GetBoolean());
        Assert.AreEqual(0, detail.GetProperty("sessions").GetArrayLength());
    }

    [TestMethod]
    public void RoleCorrelationIsExplicitBoundedAndOptIn()
    {
        var id = Guid.NewGuid();
        var disabled = new SqlCleanupDiagnostics(_ => null, _ => Assert.Fail());
        disabled.TrackConnection(id, SqlFixtureConnectionRole.SetupOwnership);
        Assert.AreEqual("Unavailable", disabled.ConnectionRole(id));
        var enabled = new SqlCleanupDiagnostics(_ => "1", _ => { });
        var serverIdentity = new Mock<DbDataReader>();
        serverIdentity.Setup(x => x.NextResult()).Returns(true);
        serverIdentity.Setup(x => x.Read()).Returns(true);
        serverIdentity.Setup(x => x.GetGuid(0)).Returns(id);
        enabled.TrackConnection(serverIdentity.Object, SqlFixtureConnectionRole.SetupOwnership);
        Assert.AreEqual("SetupOwnership", enabled.ConnectionRole(id));
        var failedTracking = new SqlCleanupDiagnostics(_ => "1", _ => { });
        var failedIdentity = new Mock<DbDataReader>();
        failedIdentity.Setup(x => x.NextResult()).Throws(new IOException("synthetic identity observation"));
        failedTracking.TrackConnection(failedIdentity.Object, SqlFixtureConnectionRole.SetupMaster);
        Assert.AreEqual("Unavailable", failedTracking.ConnectionRole(Guid.NewGuid()));
        foreach (var role in Enum.GetValues<SqlFixtureConnectionRole>())
        {
            id = Guid.NewGuid(); enabled.TrackConnection(id, role);
            Assert.AreEqual(role.ToString(), enabled.ConnectionRole(id));
        }
        Assert.AreEqual("Unmatched", enabled.ConnectionRole(Guid.NewGuid()));
        for (var i = 0; i < SqlCleanupDiagnostics.MaximumConnectionRoles + 1; i++)
            enabled.TrackConnection(Guid.NewGuid(), SqlFixtureConnectionRole.OwnedFixture);
        Assert.AreEqual("Unavailable", enabled.ConnectionRole(Guid.NewGuid()));
        Assert.AreEqual("Unavailable", enabled.ConnectionRole(null));
    }

    [TestMethod]
    public void UnknownStatusAndMissingIdentityCannotLeakOrImplyAMatch()
    {
        const string canary = "sensitive-status-canary";
        var output = new List<string>(); var diagnostics = new SqlCleanupDiagnostics(_ => "1", output.Add);
        Assert.AreEqual(1, diagnostics.InspectGuard(() => Observation(1, 1, null, canary)));
        var row = Details(diagnostics, output).GetProperty("sessions")[0];
        Assert.AreEqual("unknown", row.GetProperty("status").GetString());
        Assert.AreEqual("Unavailable", row.GetProperty("fixtureRole").GetString());
        Assert.IsFalse(output.Last().Contains(canary, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task MetadataAndDisposalFailureRetainCountAndOriginalCleanupFailure()
    {
        var output = new List<string>(); var diagnostics = new SqlCleanupDiagnostics(_ => "1", output.Add);
        var reader = new Mock<DbDataReader>(); reader.Setup(x => x.Read()).Returns(true);
        reader.Setup(x => x.GetInt32(0)).Returns(1);
        reader.Setup(x => x.NextResult()).Throws(new IOException("synthetic metadata failure"));
        reader.Protected().Setup("Dispose", ItExpr.IsAny<bool>()).Throws(new IOException("synthetic disposal failure"));
        Assert.AreEqual(1, diagnostics.InspectGuard(() => reader.Object));
        Assert.AreEqual("unavailable", Details(diagnostics, output).GetProperty("availability").GetString());
        var original = new InvalidOperationException("synthetic guard refusal");
        var caught = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            SqlCleanupDiagnostics.RunCleanupAsync(() => Task.FromException(original), _ => throw new IOException("synthetic sink")));
        Assert.AreSame(original, caught);
    }

    [TestMethod]
    public void QueryFailureIsNotReplacedByReaderDisposalFailure()
    {
        var output = new List<string>();
        var diagnostics = new SqlCleanupDiagnostics(_ => "1", output.Add);
        Assert.AreEqual(0, diagnostics.InspectGuard(() => Observation(0, 0, null)));
        var original = new InvalidOperationException("synthetic query failure");
        var reader = new Mock<DbDataReader>(); reader.Setup(x => x.Read()).Throws(original);
        reader.Protected().Setup("Dispose", ItExpr.IsAny<bool>()).Throws(new IOException("synthetic disposal failure"));
        var caught = Assert.ThrowsException<InvalidOperationException>(() => diagnostics.InspectGuard(() => reader.Object));
        Assert.AreSame(original, caught);
        Assert.AreEqual("unavailable", Details(diagnostics, output).GetProperty("availability").GetString());
    }
}

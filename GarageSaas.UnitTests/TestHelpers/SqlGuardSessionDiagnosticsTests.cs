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

    private static DbDataReader LifecycleObservation(int sessionCount = 1, int transactionRows = 1, int open = 1,
        string status = "sleeping", int type = 3, int state = 2, bool userTransaction = false, bool target = true)
    {
        var sessions = new DataTable();
        foreach (var column in new[] { ("id", typeof(int)), ("login", typeof(DateTime)), ("user", typeof(bool)),
            ("status", typeof(string)), ("request", typeof(bool)), ("requestStatus", typeof(string)),
            ("target", typeof(bool)), ("requestTarget", typeof(bool)), ("open", typeof(int)), ("connection", typeof(Guid)) })
            sessions.Columns.Add(column.Item1, column.Item2);
        var transactions = new DataTable();
        foreach (var column in new[] { ("id", typeof(int)), ("user", typeof(bool)), ("type", typeof(int)), ("state", typeof(int)), ("target", typeof(bool)) })
            transactions.Columns.Add(column.Item1, column.Item2);
        for (int i = 0; i < sessionCount; i++) {
            sessions.Rows.Add(7100+i, new DateTime(2026, 1, 1), false, status, false, DBNull.Value, target, DBNull.Value, open, DBNull.Value);
            for (int j = 0; j < transactionRows; j++) transactions.Rows.Add(7100+i, userTransaction, type, state, target);
        }
        return new DataTableReader(new[] { sessions, transactions });
    }
    private static JsonElement LifecycleRecord(SqlCleanupDiagnostics diagnostics, List<string> output)
    {
        diagnostics.Record("ActiveSessionGuard", new InvalidOperationException("synthetic"), true, true,
            false, true, 1, "private-database-canary", new SqlTestWorkers(), () => throw new AssertFailedException());
        Assert.IsFalse(output.Last().Contains("7100", StringComparison.Ordinal));
        Assert.IsFalse(output.Last().Contains("private-database-canary", StringComparison.Ordinal));
        using var json = JsonDocument.Parse(output.Last()["SQL-CLEANUP-DIAGNOSTIC:".Length..]);
        return json.RootElement.GetProperty("lifecycle").Clone();
    }
    private static void CheckLifecyclePhases()
    {
        var output = new List<string>(); var d = new SqlCleanupDiagnostics(_ => "1", output.Add);
        d.RegisterScenario(SqlCleanupDiagnostics.Scenario.NewRetargetNone);
        d.ObserveLifecycle("PreOwnership", () => LifecycleObservation(2));
        d.ObserveLifecycle("Guard", () => LifecycleObservation());
        d.ObserveLifecycle("PostRefusal", () => LifecycleObservation(0));
        var l = LifecycleRecord(d, output); Assert.IsFalse(l.GetProperty("bodyCompleted").GetBoolean());
        var phases = l.GetProperty("observations"); Assert.AreEqual(3, phases.GetArrayLength());
        Assert.IsFalse(phases[0].GetProperty("sessions")[0].GetProperty("transactions")[0].GetProperty("userTransaction").GetBoolean());
        Assert.AreEqual(1, d.Correlate(7101, new DateTime(2026,1,1))); // Private identities cleared after emission.
        Assert.AreEqual(2, phases[0].GetProperty("sessions").GetArrayLength());
        Assert.AreEqual(0, phases[2].GetProperty("sessions").GetArrayLength());
        Assert.AreEqual(phases[0].GetProperty("sessions")[0].GetProperty("correlation").GetInt32(), phases[1].GetProperty("sessions")[0].GetProperty("correlation").GetInt32());
        for (int type = 1; type <= 5; type++) for (int state = 0; state <= 9; state++) {
            output.Clear(); d = new SqlCleanupDiagnostics(_ => "1", output.Add); d.RegisterScenario(SqlCleanupDiagnostics.Scenario.UpdateQuoteNone);
            d.ObserveLifecycle("Guard", () => LifecycleObservation(type: type, state: state, userTransaction: true, target: false));
            var row = LifecycleRecord(d, output).GetProperty("observations")[0].GetProperty("sessions")[0];
            Assert.AreEqual("other", row.GetProperty("sessionDatabaseAssociation").GetString());
            Assert.AreEqual("unavailable", row.GetProperty("requestStatus").GetString());
            var t = row.GetProperty("transactions")[0]; Assert.IsTrue(t.GetProperty("userTransaction").GetBoolean());
            Assert.AreEqual(new[] { "readWrite", "readOnly", "system", "distributed", "unavailable" }[type-1], t.GetProperty("type").GetString());
            Assert.AreEqual(new[] { "initializing", "initialized", "active", "ended", "commitInitiated", "prepared", "committed", "rollingBack", "rolledBack", "unavailable" }[state], t.GetProperty("state").GetString());
        }
        foreach (var pair in new[] { (0,0), (0,1), (1,1), (3,3), (5,7) }) {
            output.Clear(); d = new SqlCleanupDiagnostics(_ => "1", output.Add); d.RegisterScenario(SqlCleanupDiagnostics.Scenario.NewQuoteNone);
            d.ObserveLifecycle("Guard", () => LifecycleObservation(transactionRows: pair.Item1, open: pair.Item2));
            var row = LifecycleRecord(d, output).GetProperty("observations")[0].GetProperty("sessions")[0];
            Assert.AreEqual(Math.Min(pair.Item1,4), row.GetProperty("transactions").GetArrayLength());
            Assert.AreEqual(pair.Item1 > 4, row.GetProperty("transactionsTruncated").GetBoolean());
            Assert.AreEqual(pair != (0,1), row.GetProperty("transactionsAvailability").GetString() == "available");
        }
        output.Clear(); d = new SqlCleanupDiagnostics(_ => "1", output.Add); d.RegisterScenario(SqlCleanupDiagnostics.Scenario.NewQuoteNone);
        d.ObserveLifecycle("Guard", () => LifecycleObservation(17, 0, 0));
        var capped = LifecycleRecord(d, output).GetProperty("observations")[0];
        Assert.AreEqual(16, capped.GetProperty("sessions").GetArrayLength()); Assert.IsTrue(capped.GetProperty("truncated").GetBoolean());
        output.Clear(); d = new SqlCleanupDiagnostics(_ => "1", output.Add); d.RegisterScenario(SqlCleanupDiagnostics.Scenario.NewQuoteNone);
        foreach (var phase in new[] { "PreOwnership", "Guard", "PostRefusal" })
            d.ObserveLifecycle(phase, () => LifecycleObservation(17, 5, 7));
        var bounded = LifecycleRecord(d, output);
        Assert.AreEqual("unavailable", bounded.GetProperty("availability").GetString());
        Assert.AreEqual(0, bounded.GetProperty("observations").GetArrayLength());
        Assert.IsTrue(output.Last().Length <= 16408); // Existing 16KiB JSON transport plus marker.

    }

    [TestMethod]
    public void MaterializedCountIsNotCappedAndPrivateIdsAreNotEmitted()
    {
        CheckLifecyclePhases();
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
        var correlation = new SqlCleanupDiagnostics(_ => "1", _ => { });
        var login = new DateTime(2026,1,1);
        Assert.AreEqual(1, correlation.Correlate(7100, login)); Assert.AreEqual(1, correlation.Correlate(7100, login));
        Assert.IsNull(correlation.Correlate(7100, login.AddSeconds(1))); Assert.IsNull(correlation.Correlate(7100, login));
        Assert.IsNull(correlation.Correlate(7101, null)); Assert.IsNull(correlation.Correlate(7101, new DateTime(1900,1,1)));
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
        diagnostics.RegisterScenario(SqlCleanupDiagnostics.Scenario.NewQuoteNone);
        diagnostics.ObserveLifecycle("Guard", () => LifecycleObservation(status: canary, open: -1));
        var safe = LifecycleRecord(diagnostics, output).GetProperty("observations")[0].GetProperty("sessions")[0];
        Assert.AreEqual("unavailable", safe.GetProperty("sessionStatus").GetString());
        Assert.AreEqual(JsonValueKind.Null, safe.GetProperty("openTransactions").ValueKind);
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
        diagnostics.RegisterScenario(SqlCleanupDiagnostics.Scenario.NewQuoteNone);
        diagnostics.ObserveLifecycle("Guard", () => throw new IOException("private-query-canary"));
        var lifecycle = LifecycleRecord(diagnostics, output);
        Assert.AreEqual("unavailable", lifecycle.GetProperty("observations")[0].GetProperty("availability").GetString());
        Assert.IsFalse(output.Last().Contains("private-query-canary", StringComparison.Ordinal));
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

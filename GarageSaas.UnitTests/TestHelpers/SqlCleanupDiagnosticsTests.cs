using System.Runtime.CompilerServices;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GarageSaas.UnitTests.TestHelpers;

[TestClass]
public class SqlCleanupDiagnosticsTests
{
    private sealed class FailingDisposal : IDisposable { public void Dispose() => throw new IOException("synthetic disposal"); }

    private sealed class Disposal(Action action) : IDisposable { public void Dispose() => action(); }
    private static SqlException SyntheticSql(params int[] numbers)
    {
        // Synthetic exception construction only: no connections or SQL operations.
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), true)!;
        var constructor = typeof(SqlError).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).OrderBy(x => x.GetParameters().Length).First();
        foreach (var number in numbers) {
            var args = constructor.GetParameters().Select((p,i) => p.ParameterType == typeof(int) ? (object)(i == 0 ? number : 0)
                : p.ParameterType == typeof(byte) ? (byte)0 : p.ParameterType == typeof(uint) ? (uint)0
                : p.ParameterType == typeof(string) ? "synthetic-private-canary" : p.ParameterType == typeof(bool) ? false : null).ToArray();
            var error = (SqlError)constructor.Invoke(args);
            typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(errors, new object[] { error });
        }
        var factory = typeof(SqlException).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Single(m => m.Name == "CreateException" &&
            m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(SqlErrorCollection) && m.GetParameters()[1].ParameterType == typeof(string));
        return (SqlException)factory.Invoke(null, new object[] { errors, "synthetic" })!;
    }
    private static JsonElement StepRecord(SqlCleanupDiagnostics d, List<string> lines, string section)
    {
        d.Record("OpenOwnedDatabase", new InvalidOperationException("synthetic-private-canary"), true, true,
            true, false, null, "synthetic-private-canary", new SqlTestWorkers(), () => throw new AssertFailedException("No SQL"));
        Assert.IsFalse(lines.Last().Contains("synthetic-private-canary", StringComparison.Ordinal));
        using var json = JsonDocument.Parse(lines.Last()["SQL-CLEANUP-DIAGNOSTIC:".Length..]);
        return json.RootElement.GetProperty(section).Clone();
    }
    private static void CheckHostedCategories()
    {
        foreach (var pair in new[] { ("DB STARTUP","DatabaseStartup"),("CHECKPOINT","Checkpoint"),("LAZY WRITER","LazyWriter"),
            ("LOG WRITER","LogWriter"),("RECOVERY WRITER","RecoveryWriter"),("CREATE DATABASE","DatabaseCreate") })
            Assert.AreEqual(pair.Item2,SqlCleanupDiagnostics.RequestCategory(pair.Item1));
        foreach(var pair in new[] { ("WRITELOG","IO"),("LCK_M_X","Lock"),("PAGELATCH_EX","Latch"),("SOS_SCHEDULER_YIELD","Scheduler") })
            Assert.AreEqual(pair.Item2,SqlCleanupDiagnostics.WaitCategory(pair.Item1));
        foreach(var hostile in new[] { "synthetic-private-canary",new string('x',20000),"CHECKPOINT;secret", "" }) {
            Assert.AreEqual("Unknown",SqlCleanupDiagnostics.RequestCategory(hostile));
            Assert.AreEqual("Unknown",SqlCleanupDiagnostics.WaitCategory(hostile));
        }
        Assert.AreEqual("Unknown",SqlCleanupDiagnostics.RequestCategory(null));
        Assert.AreEqual("Unknown",SqlCleanupDiagnostics.WaitCategory(null));
        Assert.AreEqual("Other",SqlCleanupDiagnostics.SafeExceptionType(new Exception("synthetic-private-canary",SyntheticSql(-2))));
        var nested=new Exception("synthetic-private-canary",SyntheticSql(-2));
        var text=JsonSerializer.Serialize(SqlCleanupDiagnostics.StepEvidence("None","DatabaseCreate",nested,0));
        Assert.IsFalse(text.Contains("synthetic-private-canary",StringComparison.Ordinal));
        using(var json=JsonDocument.Parse(text)) Assert.AreEqual(-2,json.RootElement.GetProperty("sqlErrorNumbers")[0].GetInt32());
        foreach(var stage in Enum.GetValues<SqlCleanupDiagnostics.SeedStage>()) {
            var lines=new List<string>();var diagnostic=new SqlCleanupDiagnostics(_=>"1",lines.Add);diagnostic.RegisterCustomerDiagnostics();
            var original=new InvalidOperationException("synthetic-private-canary");
            Assert.AreSame(original,Assert.ThrowsException<InvalidOperationException>(()=>diagnostic.SeedStep(SqlCleanupDiagnostics.SeedGraph.None,stage,()=>throw original)));
            var row=StepRecord(diagnostic,lines,"seeding");Assert.AreEqual(stage.ToString(),row.GetProperty("stage").GetString());
            Assert.AreEqual("InvalidOperationException",row.GetProperty("exceptionType").GetString());
        }
    }
    private static void CheckStepDiagnostics()
    {
        CheckHostedCategories();
        foreach (var graph in new[] { SqlCleanupDiagnostics.SeedGraph.First, SqlCleanupDiagnostics.SeedGraph.Second })
        foreach (var stage in Enum.GetValues<SqlCleanupDiagnostics.SeedStage>()) {
            var lines = new List<string>(); var d = new SqlCleanupDiagnostics(_ => "1", lines.Add); d.RegisterCustomerDiagnostics();
            var error = new InvalidOperationException("synthetic-private-canary");
            var caught = Assert.ThrowsException<InvalidOperationException>(() => d.SeedStep(graph, stage, () => throw error));
            Assert.AreSame(error, caught);
            var row = StepRecord(d, lines, "seeding"); Assert.AreEqual(graph.ToString(), row.GetProperty("graph").GetString());
            Assert.AreEqual(stage.ToString(), row.GetProperty("stage").GetString()); Assert.IsFalse(row.GetProperty("succeeded").GetBoolean());
        }
        foreach (var stage in Enum.GetValues<SqlCleanupDiagnostics.OpenStage>()) {
            var lines = new List<string>(); var d = new SqlCleanupDiagnostics(_ => "1", lines.Add); d.RegisterCustomerDiagnostics();
            if (stage == SqlCleanupDiagnostics.OpenStage.Completed) d.OpenStep(stage, () => 42);
            else {
                var error = new IOException("synthetic-private-canary");
                Assert.AreSame(error, Assert.ThrowsException<IOException>(() => d.OpenStep(stage, () => throw error)));
            }
            var row = StepRecord(d, lines, "cleanupOpen"); Assert.AreEqual(stage.ToString(), row.GetProperty("stage").GetString());
            Assert.AreEqual(stage == SqlCleanupDiagnostics.OpenStage.Completed, row.GetProperty("succeeded").GetBoolean());
        }
        foreach (var pair in new (Exception Error, string Category)[] {
            (SyntheticSql(-2), "Sql"), (new TimeoutException("synthetic-private-canary"), "Timeout"),
            (new OperationCanceledException("synthetic-private-canary"), "Cancelled") }) {
            var lines = new List<string>(); var d = new SqlCleanupDiagnostics(_ => "1", lines.Add); d.RegisterCustomerDiagnostics();
            try { d.SeedStep(SqlCleanupDiagnostics.SeedGraph.None, SqlCleanupDiagnostics.SeedStage.ContextConstruction, () => throw pair.Error); }
            catch (Exception caught) { Assert.AreSame(pair.Error, caught); }
            var row = StepRecord(d, lines, "seeding"); Assert.AreEqual(pair.Category, row.GetProperty("category").GetString());
            if (pair.Category == "Sql") Assert.AreEqual(-2, row.GetProperty("sqlErrorNumbers")[0].GetInt32());
        }
        var sql = SyntheticSql(-2, 1205, 3702, 4060, 18456, -1);
        var wrapped = new DbUpdateException("synthetic-private-canary", sql);
        var output = new List<string>(); var observed = new SqlCleanupDiagnostics(_ => "1", output.Add); observed.RegisterCustomerDiagnostics();
        Assert.AreSame(wrapped, Assert.ThrowsException<DbUpdateException>(() => observed.SeedStep(SqlCleanupDiagnostics.SeedGraph.First,
            SqlCleanupDiagnostics.SeedStage.GraphConstruction, () => observed.SeedStep(SqlCleanupDiagnostics.SeedGraph.First,
                SqlCleanupDiagnostics.SeedStage.GarageSave, () => throw wrapped))));
        observed.SeedStep(SqlCleanupDiagnostics.SeedGraph.None, SqlCleanupDiagnostics.SeedStage.ContextDispose, () => { });
        var evidence = StepRecord(observed, output, "seeding");
        Assert.AreEqual("GarageSave", evidence.GetProperty("stage").GetString()); Assert.AreEqual("DbUpdate", evidence.GetProperty("category").GetString());
        Assert.AreEqual(4, evidence.GetProperty("sqlErrorNumbers").GetArrayLength()); Assert.AreEqual(-2, evidence.GetProperty("sqlErrorNumbers")[0].GetInt32());
        Assert.IsTrue(evidence.GetProperty("sqlErrorsTruncated").GetBoolean());
        foreach (var duration in new[] { -1L, 0, 600000, 600001, long.MaxValue }) {
            var result = SqlCleanupDiagnostics.BoundDuration(duration); Assert.IsTrue(result.Milliseconds >= 0 && result.Milliseconds <= 600000);
            Assert.AreEqual(duration > 600000, result.Capped);
        }
        var calls = 0; var disposalError = new IOException("synthetic-private-canary");
        Assert.AreSame(disposalError, Assert.ThrowsException<IOException>(() => {
            using var lifetime = observed.OpenDisposal(true, SqlCleanupDiagnostics.OpenStage.ReaderDispose,
                new Disposal(() => { calls++; throw disposalError; }));
        }));
        Assert.AreEqual(1, calls); Assert.AreEqual("ReaderDispose", StepRecord(observed, output, "cleanupOpen").GetProperty("stage").GetString());
        var lifecycle = new SqlCleanupDiagnostics(_ => "1", _ => { }); lifecycle.RegisterScenario(SqlCleanupDiagnostics.Scenario.NewQuoteNone);
        lifecycle.RegisterCustomerDiagnostics(); Assert.IsFalse(lifecycle.CustomerDiagnosticsEnabled);
        var customer = new SqlCleanupDiagnostics(_ => "1", _ => { }); customer.RegisterCustomerDiagnostics();
        customer.RegisterScenario(SqlCleanupDiagnostics.Scenario.NewQuoteNone); Assert.IsFalse(customer.LifecycleEnabled);
        using var db = new SignupContext(new DbContextOptionsBuilder<SignupContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var order = new List<SqlCleanupDiagnostics.SeedStage>();
        SqlInvoiceGraph.Seed(db, (stage, save) => { order.Add(stage); Assert.IsNull(db.Database.CurrentTransaction); save(); });
        CollectionAssert.AreEqual(new[] { SqlCleanupDiagnostics.SeedStage.GarageSave, SqlCleanupDiagnostics.SeedStage.CustomerVehicleSave,
            SqlCleanupDiagnostics.SeedStage.OwnershipWorkItemSave }, order);
    }

    [TestMethod]
    public void DisabledByDefaultDoesNotCollectOrEmit()
    {
        var diagnostics = new SqlCleanupDiagnostics(_ => null, _ => Assert.Fail("Unexpected output"));
        Assert.IsFalse(diagnostics.Enabled);
        diagnostics.RegisterCustomerDiagnostics(); var calls = 0;
        diagnostics.SeedStep(SqlCleanupDiagnostics.SeedGraph.First, SqlCleanupDiagnostics.SeedStage.GarageSave, () => calls++);
        diagnostics.OpenStep(SqlCleanupDiagnostics.OpenStage.ConnectionOpen, () => calls++);
        Assert.AreEqual(2, calls); Assert.IsFalse(diagnostics.CustomerDiagnosticsEnabled);
        diagnostics.Record("Synthetic", null, true, true, true, true, 0, "unused",
            new SqlTestWorkers(), () => throw new InvalidOperationException("Must not collect"));
    }

    [TestMethod]
    public void OnlyExplicitOneEnablesDiagnostics()
    {
        Assert.IsTrue(new SqlCleanupDiagnostics(_ => "1", _ => { }).Enabled);
        foreach (var value in new[] { "", "0", "true", " 1" })
            Assert.IsFalse(new SqlCleanupDiagnostics(_ => value, _ => { }).Enabled);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task ThrowOriginal(Exception original) => throw original;

    [TestMethod]
    public async Task CleanupFailurePreservesOriginalAndStack()
    {
        CheckStepDiagnostics();
        var original = new InvalidOperationException("synthetic");
        Exception? reported = null;
        var caught = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            SqlCleanupDiagnostics.RunCleanupAsync(() => ThrowOriginal(original), error => reported = error));
        Assert.AreSame(original, caught);
        Assert.AreSame(original, reported);
        StringAssert.Contains(caught.StackTrace!, nameof(ThrowOriginal));
    }

    [TestMethod]
    public async Task DiagnosticFailureCannotReplaceCleanupFailure()
    {
        var original = new InvalidOperationException("synthetic");
        var caught = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            SqlCleanupDiagnostics.RunCleanupAsync(() => ThrowOriginal(original), _ => throw new IOException("sink")));
        Assert.AreSame(original, caught);
        StringAssert.Contains(caught.StackTrace!, nameof(ThrowOriginal));
        Assert.AreEqual(typeof(IOException).FullName, caught.Data["CleanupDiagnosticFailureType"]);
    }

    [TestMethod]
    public async Task OutputRedactsSensitiveExceptionAndSnapshotValues()
    {
        const string sensitive = "synthetic-private-canary";
        var lines = new List<string>();
        var diagnostics = new SqlCleanupDiagnostics(_ => "1", lines.Add);
        diagnostics.RegisterScenario(SqlCleanupDiagnostics.Scenario.NewRetargetDirect);
        var original = new InvalidOperationException("Password=" + sensitive + ";AccessToken=" + sensitive);
        original.Data["Customer"] = sensitive;
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            SqlCleanupDiagnostics.RunCleanupAsync(() => ThrowOriginal(original), error =>
                diagnostics.Record("Synthetic", error, true, true, true, true, 0, sensitive,
                    new SqlTestWorkers(), () => throw new IOException(sensitive))));
        Assert.AreEqual(1, lines.Count);
        Assert.IsFalse(lines[0].Contains(sensitive, StringComparison.Ordinal));
        Assert.IsFalse(lines[0].Contains("Password=", StringComparison.Ordinal));
        Assert.IsFalse(lines[0].Contains("AccessToken=", StringComparison.Ordinal));
        using var json = JsonDocument.Parse(lines[0]["SQL-CLEANUP-DIAGNOSTIC:".Length..]);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, json.RootElement.GetProperty("exceptionType").GetString());
        Assert.AreEqual(typeof(IOException).FullName, json.RootElement.GetProperty("snapshotExceptionType").GetString());
        Assert.IsTrue(json.RootElement.GetProperty("stack").GetArrayLength() > 0);
        Assert.IsFalse(json.RootElement.GetProperty("lifecycle").GetProperty("bodyCompleted").GetBoolean());
        diagnostics.BodyCompleted();
        diagnostics.Record("Completed", null, false, true, false, true, 0, sensitive, new SqlTestWorkers(), () => throw new IOException());
        using var completed = JsonDocument.Parse(lines.Last()["SQL-CLEANUP-DIAGNOSTIC:".Length..]);
        Assert.IsTrue(completed.RootElement.GetProperty("lifecycle").GetProperty("bodyCompleted").GetBoolean());
    }

    [TestMethod]
    public async Task SuccessfulCleanupRunsOnceAndReportsAfterCompletion()
    {
        var calls = 0;
        var reports = 0;
        await SqlCleanupDiagnostics.RunCleanupAsync(() => { calls++; return Task.CompletedTask; }, error =>
        {
            Assert.AreEqual(1, calls);
            Assert.IsNull(error);
            reports++;
        });
        Assert.AreEqual(1, calls);
        Assert.AreEqual(1, reports);
        var output = new List<string>(); var diagnostics = new SqlCleanupDiagnostics(_ => "1", output.Add);
        diagnostics.RegisterScenario(SqlCleanupDiagnostics.Scenario.NewQuoteNone);
        Assert.ThrowsException<IOException>(() => {
            using (var scope = new FailingDisposal()) { }
            diagnostics.BodyCompleted(); // Must remain unreachable after failed disposal.
        });
        diagnostics.Record("Completed", null, false, true, false, false, null, "unused", new SqlTestWorkers(), () => throw new IOException());
        using var json = JsonDocument.Parse(output.Last()["SQL-CLEANUP-DIAGNOSTIC:".Length..]);
        Assert.IsFalse(json.RootElement.GetProperty("lifecycle").GetProperty("bodyCompleted").GetBoolean());
    }

    [TestMethod]
    public async Task FailedDiagnosticSinkCannotChangeSuccessfulCleanup()
    {
        CheckHostedCategories();
        var calls = 0;
        await SqlCleanupDiagnostics.RunCleanupAsync(() => { calls++; return Task.CompletedTask; },
            _ => throw new IOException("synthetic sink failure"));
        Assert.AreEqual(1, calls);
    }
}

using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GarageSaas.UnitTests.TestHelpers;

// Called by the existing diagnostic test: preserves all discovered inventories.
// Deterministic synthetic gates; never opens SQL or changes a server session.
internal static class SqlPendingDropDiagnosticChecks
{
    private static SqlPendingDropDiagnostics.Observation Captured() => new("Available", "Captured", "DropDatabase",
        "Suspended", "Lock", "SystemSession", "Checkpoint", 0, "None", null, 1);
    private static JsonElement Evidence(SqlPendingDropDiagnostics value) => JsonSerializer.SerializeToElement(value.Evidence);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OriginalFailure(Exception error) => throw error;
    private static async Task<SqlPendingDropDiagnostics> Pending(Func<CancellationToken, Task<SqlPendingDropDiagnostics.Observation>> probe, Exception? original = null)
    {
        var observer = new SqlPendingDropDiagnostics();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sample = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var work = Task.Run(() => observer.Execute(() => {
            calls++; entered.SetResult(); release.Task.GetAwaiter().GetResult();
            if (original != null) OriginalFailure(original);
        }, probe, token => sample.Task.WaitAsync(token)));
        try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); sample.SetResult(); await observer.Watcher.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { release.TrySetResult(); }
        if (original == null) await work.WaitAsync(TimeSpan.FromSeconds(5));
        else {
            try { await work.WaitAsync(TimeSpan.FromSeconds(5)); Assert.Fail("Original failure was lost"); }
            catch (Exception caught) { Assert.AreSame(original, caught); StringAssert.Contains(caught.StackTrace!, nameof(OriginalFailure)); }
        }
        Assert.AreEqual(1, calls);
        return observer;
    }
    internal static async Task Run(Func<int[], SqlException> sql)
    {
        var fast = new SqlPendingDropDiagnostics(); var probeCalls = 0; var drops = 0;
        fast.Execute(() => drops++, _ => { probeCalls++; return Task.FromResult(Captured()); });
        await fast.Watcher.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, drops); Assert.AreEqual(0, probeCalls);
        Assert.AreEqual("Unavailable", Evidence(fast).GetProperty("observation").GetProperty("availability").GetString());
        Assert.IsFalse(Evidence(fast).GetProperty("bodyCompleted").GetBoolean());
        fast.BodyCompleted(); Assert.IsTrue(Evidence(fast).GetProperty("bodyCompleted").GetBoolean());

        var successful = await Pending(_ => Task.FromResult(Captured()));
        Assert.AreEqual("Available", Evidence(successful).GetProperty("observation").GetProperty("availability").GetString());
        var original = sql(new[] { -2, 0 });
        var failed = await Pending(_ => Task.FromResult(Captured()), original);
        Assert.AreEqual("Available", Evidence(failed).GetProperty("observation").GetProperty("availability").GetString());
        foreach (var error in new Exception[] { new IOException("private-canary"), sql(new[] { -2, 0, 999999 }),
            new TimeoutException("private-canary"), new OperationCanceledException("private-canary"),
            new InvalidOperationException("private-canary", sql(new[] { -2 })) }) {
            var observed = await Pending(_ => throw error, original);
            var json = JsonSerializer.Serialize(observed.Evidence);
            Assert.IsFalse(json.Contains("private-canary", StringComparison.Ordinal)); Assert.IsFalse(json.Contains("999999", StringComparison.Ordinal));
            Assert.AreEqual("Unavailable", Evidence(observed).GetProperty("observation").GetProperty("availability").GetString());
            await Pending(_ => throw error); // Observer failure cannot fail a successful DROP.
        }
        foreach (var number in new[] { -2, 0, 53, 233, 229, 297, 300 }) {
            var row = SqlPendingDropDiagnostics.Failure(sql(new[] { number })); Assert.AreEqual(number, row.sqlErrorNumbers![0]);
        }
        Assert.AreEqual("Unknown", SqlPendingDropDiagnostics.Status("private-canary"));
        Assert.AreEqual("Unknown", SqlPendingDropDiagnostics.Wait("private-canary"));
        Assert.AreEqual("Unknown", SqlPendingDropDiagnostics.Request("private-canary"));
        Assert.AreEqual("Unknown", SqlPendingDropDiagnostics.Blocking(int.MaxValue, null));
        foreach (var pair in new[] { ("LCK_M_SCH_M", "Lock"), ("WRITELOG", "LogIO"), ("PAGEIOLATCH_SH", "IO"),
            ("PREEMPTIVE_OS_DELETEFILE", "FileIO"), ("LATCH_EX", "Latch"), ("SOS_SCHEDULER_YIELD", "Scheduler"), ("", "NoneReported") })
            Assert.AreEqual(pair.Item2, SqlPendingDropDiagnostics.Wait(pair.Item1));
        foreach (var pair in new[] { ("DB STARTUP", "DatabaseStartup"), ("CHECKPOINT", "Checkpoint"), ("LAZY WRITER", "LazyWriter"),
            ("LOG WRITER", "LogWriter"), ("RECOVERY WRITER", "RecoveryWriter"), ("DROP DATABASE", "DropDatabase") })
            Assert.AreEqual(pair.Item2, SqlPendingDropDiagnostics.Request(pair.Item1));
        Assert.AreEqual("NoneReported", SqlPendingDropDiagnostics.Blocking(0, null));
        Assert.AreEqual("UserSession", SqlPendingDropDiagnostics.Blocking(1, true));
        Assert.AreEqual("SystemSession", SqlPendingDropDiagnostics.Blocking(1, false));
        Assert.AreEqual("OrphanedTransaction", SqlPendingDropDiagnostics.Blocking(-2, null));
        Assert.AreEqual("DeferredRecovery", SqlPendingDropDiagnostics.Blocking(-3, null));
        foreach (var code in new[] { -4, -5 }) Assert.AreEqual("InternalOwnerUnknown", SqlPendingDropDiagnostics.Blocking(code, null));
        foreach (var row in new[] { Captured() with { status = "private-canary" }, Captured() with { wait = "private-canary" },
            Captured() with { blockerRequest = "private-canary" }, Captured() with { elapsedMilliseconds = 2001 },
            Captured() with { transactions = -1 }, Captured() with { sqlErrorNumbers = new[] { -2 } },
            SqlPendingDropDiagnostics.Unavailable("private-canary"), SqlPendingDropDiagnostics.Unavailable("ObserverFailed", "Sql", new[] { -2, -2 }) }) {
            var clean = SqlPendingDropDiagnostics.Sanitize(row);
            Assert.AreEqual("Unavailable", clean.availability); Assert.IsFalse(JsonSerializer.Serialize(clean).Contains("private-canary", StringComparison.Ordinal));
        }
        // A noncooperative, late observer cannot postpone or rewrite DROP success.
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource<SqlPendingDropDiagnostics.Observation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new SqlPendingDropDiagnostics();
        pending.Execute(() => started.Task.GetAwaiter().GetResult(), _ => { started.SetResult(); return late.Task; }, _ => Task.CompletedTask);
        await pending.Watcher.WaitAsync(TimeSpan.FromSeconds(5));
        var frozen = JsonSerializer.Serialize(pending.Evidence); late.SetResult(Captured());
        Assert.AreEqual(frozen, JsonSerializer.Serialize(pending.Evidence));
        Assert.AreEqual("Unavailable", Evidence(pending).GetProperty("observation").GetProperty("availability").GetString());

        var lateFailureStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateFailure = new TaskCompletionSource<SqlPendingDropDiagnostics.Observation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failingDrop = new SqlPendingDropDiagnostics();
        var same = Assert.ThrowsException<SqlException>(() => failingDrop.Execute(() => {
            lateFailureStarted.Task.GetAwaiter().GetResult(); OriginalFailure(original);
        }, _ => { lateFailureStarted.SetResult(); return lateFailure.Task; }, _ => Task.CompletedTask));
        Assert.AreSame(original, same); await failingDrop.Watcher.WaitAsync(TimeSpan.FromSeconds(5));
        var frozenFailure = JsonSerializer.Serialize(failingDrop.Evidence);
        lateFailure.SetException(new IOException("private-canary"));
        Assert.AreEqual(frozenFailure, JsonSerializer.Serialize(failingDrop.Evidence));

        // No connection can open when correlation/validated master is unavailable.
        using var closed = new SqlConnection();
        Assert.AreEqual("CorrelationUnavailable", (await SqlPendingDropDiagnostics.Probe(closed, Guid.Empty)(CancellationToken.None)).reason);
        var disabled = new SqlCleanupDiagnostics(_ => null, _ => Assert.Fail());
        disabled.RegisterPendingDrop(); Assert.IsFalse(disabled.PendingDropEnabled);
        disabled.ObserveDrop(() => drops++, closed); Assert.AreEqual(2, drops);
        var observerConnection = new SqlConnectionStringBuilder(SqlPendingDropDiagnostics.ObserverConnection(@"np:\\.\pipe\LOCALDB#ABC123\tsql\query"));
        Assert.AreEqual("master", observerConnection.InitialCatalog); Assert.IsTrue(observerConnection.IntegratedSecurity);
        Assert.IsFalse(observerConnection.Pooling); Assert.AreEqual(1, observerConnection.ConnectTimeout); Assert.AreEqual(0, observerConnection.ConnectRetryCount);
        foreach (var invalid in new[] { "GARVAN_ASUS", "master", "(localdb)\\GarageSaasIntegrationTests", @"np:\\remote\pipe\LOCALDB#ABC\tsql\query",
            @"np:\\.\pipe\LOCALDB#ABC\tsql\query;Initial Catalog=private-canary", "Server=private-canary;Password=private-canary" })
            Assert.ThrowsException<InvalidOperationException>(() => SqlPendingDropDiagnostics.ObserverConnection(invalid));
    }
}

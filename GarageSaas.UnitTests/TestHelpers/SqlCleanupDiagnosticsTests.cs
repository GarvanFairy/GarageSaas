using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GarageSaas.UnitTests.TestHelpers;

[TestClass]
public class SqlCleanupDiagnosticsTests
{
    [TestMethod]
    public void DisabledByDefaultDoesNotCollectOrEmit()
    {
        var diagnostics = new SqlCleanupDiagnostics(_ => null, _ => Assert.Fail("Unexpected output"));
        Assert.IsFalse(diagnostics.Enabled);
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
    }

    [TestMethod]
    public async Task FailedDiagnosticSinkCannotChangeSuccessfulCleanup()
    {
        var calls = 0;
        await SqlCleanupDiagnostics.RunCleanupAsync(() => { calls++; return Task.CompletedTask; },
            _ => throw new IOException("synthetic sink failure"));
        Assert.AreEqual(1, calls);
    }
}

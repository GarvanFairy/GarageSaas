using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GarageSaas.UnitTests.TestHelpers;

// Common experiment overlay only. Never includes exception text or SQL metadata.
internal static class SqlAbcDiagnostics
{
    internal static T Measure<T>(string phase, Func<T> action)
    {
        var clock = Stopwatch.StartNew();
        try { var value = action(); Emit(phase, "Succeeded", null, clock.ElapsedMilliseconds); return value; }
        catch (Exception original) { Emit(phase, "Failed", original, clock.ElapsedMilliseconds); throw; }
    }
    internal static void Measure(string phase, Action action) => Measure(phase, () => { action(); return true; });
    internal static IDisposable Duration(string phase) => new DurationScope(phase);
    private sealed class DurationScope(string phase) : IDisposable
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        public void Dispose() => Emit(phase, "Observed", null, clock.ElapsedMilliseconds);
    }
    private static void Emit(string phase, string outcome, Exception? error, long elapsed)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("GARAGESAAS_SQL_ABC_DIAGNOSTICS") != "1") return;
            if (phase is not ("QuoteConstruction" or "QuoteSeedSave1" or "QuoteSeedSave2" or
                "InvoiceAbsenceQuery" or "InvoiceAbsenceAssertion" or "FixtureCreateDuration" or "FixtureOpenDuration")) return;
            var category = error switch {
                null => "None", SqlException => "SqlException", DbUpdateException => "DbUpdateException",
                AssertFailedException => "AssertFailedException", TimeoutException => "TimeoutException",
                InvalidOperationException => "InvalidOperationException", _ => "Unknown" };
            // One inner level only, with a fixed SQL-number allowlist.
            var sql = error as SqlException ?? error?.InnerException as SqlException;
            var number = sql?.Number;
            object code = number is -2 or 1205 or 3702 or 4060 or 18456 or 262 or 1801 or 233 or 53 ? number.Value : "Unavailable";
            Console.WriteLine("SQL-ABC:" + JsonSerializer.Serialize(new {
                phase, outcome, exception = category, sqlNumber = code,
                durationMs = Math.Clamp(elapsed, 0, 1500000) }));
        }
        catch { /* Observability failure must never replace the original outcome. */ }
    }
}

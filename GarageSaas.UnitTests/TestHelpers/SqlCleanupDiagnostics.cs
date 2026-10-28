using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GarageSaas.UnitTests.TestHelpers;

// Investigation only. No connection strings, raw exception messages, SQL text,
// principals, query payloads or database ownership-marker values are emitted.
internal sealed class SqlCleanupDiagnostics : DbConnectionInterceptor
{
    private readonly Action<string> _write;
    internal bool Enabled { get; }
    internal SqlCleanupDiagnostics() : this(Environment.GetEnvironmentVariable, Console.WriteLine) { }
    internal SqlCleanupDiagnostics(Func<string, string?> environment, Action<string> write)
    {
        Enabled = environment("GARAGESAAS_SQL_CLEANUP_DIAGNOSTICS") == "1";
        _write = write;
    }

    // Reporting occurs after cleanup and cannot change its outcome.
    internal static async Task RunCleanupAsync(Func<Task> cleanup, Action<Exception?> report)
    {
        try { await cleanup(); }
        catch (Exception original)
        {
            try { report(original); }
            catch (Exception diagnostic) { original.Data["CleanupDiagnosticFailureType"] = diagnostic.GetType().FullName; }
            throw;
        }
        try { report(null); }
        catch (Exception) { /* Diagnostic sink failures cannot change cleanup success. */ }
    }
    private readonly ConcurrentDictionary<DbConnection, byte> _open = new();
    private int _opened, _closed;
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    { _open[connection] = 0; Interlocked.Increment(ref _opened); }
    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    { ConnectionOpened(connection, eventData); return Task.CompletedTask; }
    public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData)
    { _open.TryRemove(connection, out _); Interlocked.Increment(ref _closed); }
    public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
    { ConnectionClosed(connection, eventData); return Task.CompletedTask; }
    public override void ConnectionDisposed(DbConnection connection, ConnectionEndEventData eventData) => _open.TryRemove(connection, out _);
    public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData)
    { ConnectionDisposed(connection, eventData); return Task.CompletedTask; }

    internal void Record(string operation, Exception? error, bool created, bool marked, bool dedicated,
        bool ownershipVerified, int? guardSessions, string databaseName, SqlTestWorkers workers, Func<SqlConnection> openMaster)
    {
        if (!Enabled) return;
        var data = new Dictionary<string, object?>
        {
            ["operation"] = operation, ["success"] = error == null, ["created"] = created,
            ["marked"] = marked, ["dedicatedLocalDb"] = dedicated, ["ownershipVerified"] = ownershipVerified,
            ["pooling"] = false, ["efOpenConnections"] = _open.Count, ["efOpenedEvents"] = _opened,
            ["efClosedEvents"] = _closed, ["workers"] = workers.DiagnosticSnapshot(),
            ["guardSessions"] = guardSessions, ["exceptionType"] = error?.GetType().FullName,
            ["sanitizedMessage"] = error == null ? null : SafeMessage(error),
            ["sqlErrors"] = error is SqlException sql ? SqlCodes(sql) : null,
            ["stack"] = error == null ? null : new StackTrace(error, true).GetFrames()?.Take(14).Select(frame => new
            {
                method = frame.GetMethod()?.DeclaringType?.FullName + "." + frame.GetMethod()?.Name,
                file = Path.GetFileName(frame.GetFileName()), line = frame.GetFileLineNumber()
            }).ToArray()
        };
        if (dedicated && ownershipVerified)
        {
            try
            {
                // Master metadata only, restricted to this already verified owned
                // catalog. No target data, SQL text or login details are selected.
                using var master = openMaster();
                using var command = master.CreateCommand(); command.CommandTimeout = 8;
                command.CommandText = "SELECT state_desc, user_access_desc FROM sys.databases WHERE name=@name";
                command.Parameters.AddWithValue("@name", databaseName);
                using (var reader = command.ExecuteReader())
                {
                    var present = reader.Read(); data["databasePresent"] = present;
                    data["databaseState"] = present ? reader.GetString(0) : null;
                    data["databaseAccess"] = present ? reader.GetString(1) : null;
                }
                command.CommandText = @"SELECT s.session_id, s.is_user_process, s.status, s.open_transaction_count,
                    (SELECT COUNT(*) FROM sys.dm_exec_requests r WHERE r.session_id=s.session_id),
                    CASE WHEN s.program_name=N'GarageSaas disposable SQL tests' THEN 1 ELSE 0 END
                    FROM sys.dm_exec_sessions s WHERE s.database_id=DB_ID(@name) AND s.session_id<>@@SPID";
                var sessions = new List<object>();
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) sessions.Add(new
                    {
                        sessionId = Convert.ToInt32(reader.GetValue(0)), userProcess = reader.GetBoolean(1),
                        state = reader.GetString(2), openTransactions = Convert.ToInt32(reader.GetValue(3)),
                        activeRequests = reader.GetInt32(4), fixtureApplication = reader.GetInt32(5) == 1
                    });
                data["serverSessionsAfterOutcome"] = sessions;
            }
            catch (Exception snapshotError)
            {
                // Secondary diagnostic failure is explicit and cannot suppress the
                // original cleanup exception; DeleteAsync always rethrows it.
                data["snapshotExceptionType"] = snapshotError.GetType().FullName;
                data["snapshotSqlErrors"] = snapshotError is SqlException sqlError ? SqlCodes(sqlError) : null;
            }
        }
        _write("SQL-CLEANUP-DIAGNOSTIC:" + JsonSerializer.Serialize(data));
    }

    private static object[] SqlCodes(SqlException error) => error.Errors.Cast<SqlError>()
        .Select(x => (object)new { number = x.Number, state = x.State, severity = x.Class }).ToArray();
    private static string SafeMessage(Exception error)
    {
        if (error is SqlException sql) return sql.Number switch
        {
            3702 => "SQL refused DROP DATABASE because the database is in use.",
            -2 => "SQL command timeout.", 1205 => "SQL deadlock victim.",
            _ => "SQL message suppressed; numeric error/state/severity retained."
        };
        return error.Message switch
        {
            "Database still has active sessions; refusing deletion." => error.Message,
            "Database creation/ownership was not confirmed; leaving it untouched." => error.Message,
            "Database ownership was not confirmed; refusing deletion." => error.Message,
            _ => "Message suppressed by cleanup diagnostic allowlist."
        };
    }
}

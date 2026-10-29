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
    internal enum Scenario { NewRetargetNone, UpdateRetargetNone, NewQuoteNone, UpdateQuoteNone, NewRetargetDirect, NewRetargetMixed }
    private Scenario? _scenario;
    private bool _bodyCompleted;
    private readonly List<object> _observations = new();
    private readonly HashSet<string> _phases = new(StringComparer.Ordinal);
    private readonly Dictionary<int, (DateTime Login, int Token)> _identities = new();
    private readonly HashSet<int> _ambiguous = new();
    private readonly List<int> _guardIds = new();
    private int? _lifecycleGuardCount;
    internal bool LifecycleEnabled => Enabled && _scenario.HasValue;
    internal void RegisterScenario(Scenario scenario) { if (Enabled && Enum.IsDefined(scenario)) _scenario = scenario; }
    internal void BodyCompleted() { if (LifecycleEnabled) _bodyCompleted = true; }

    // IDs and login timestamps are private correlation inputs, never serialized.
    internal int? Correlate(int id, DateTime? login)
    {
        if (id < 1 || id > 32767 || !login.HasValue || login.Value.Year <= 1900 || _ambiguous.Contains(id)) return null;
        if (_identities.TryGetValue(id, out var previous))
        {
            if (previous.Login == login.Value) return previous.Token;
            _ambiguous.Add(id); return null; // SPID reuse is not continuity.
        }
        if (_identities.Count >= 48) return null;
        var token = _identities.Count + 1; _identities.Add(id, (login.Value, token)); return token;
    }
    private static string Category(string? value, params string[] allowed)
        => allowed.Contains(value, StringComparer.Ordinal) ? value! : "unavailable";
    private static int? Bounded(int value) => value >= 0 && value <= 1000000 ? value : null;
    internal void ObserveLifecycle(string phase, Func<DbDataReader> execute)
    {
        if (!LifecycleEnabled || _observations.Count >= 3 || phase is not ("PreOwnership" or "Guard" or "PostRefusal") || !_phases.Add(phase)) return;
        var before = _observations.Count;
        try
        {
            using var reader = execute();
            var sessions = new List<object>(); var truncated = false;
            while (reader.Read())
            {
                if (sessions.Count == 16) { truncated = true; break; }
                var id = reader.GetInt32(0); var login = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
                var transactions = new List<object>();
                // Transaction rows are a separate bounded result set, consumed below.
                sessions.Add(new Dictionary<string, object?> {
                    ["correlation"] = Correlate(id, login), ["userProcess"] = reader.GetBoolean(2),
                    ["sessionStatus"] = Category(reader.GetString(3), "running", "sleeping", "dormant", "preconnect"),
                    ["hasActiveRequest"] = reader.IsDBNull(4) ? null : reader.GetBoolean(4),
                    ["requestStatus"] = reader.IsDBNull(5) ? "unavailable" : Category(reader.GetString(5), "running", "runnable", "suspended", "sleeping", "background"),
                    ["sessionDatabaseAssociation"] = reader.GetBoolean(6) ? "target" : "other",
                    ["requestDatabaseAssociation"] = reader.IsDBNull(7) ? "unavailable" : reader.GetBoolean(7) ? "target" : "other",
                    ["openTransactions"] = Bounded(reader.GetInt32(8)),
                    ["fixtureRole"] = ConnectionRole(reader.IsDBNull(9) ? null : reader.GetGuid(9)),
                    ["transactionsAvailability"] = "unavailable", ["transactionsTruncated"] = false,
                    ["transactions"] = transactions,
                    // Private keys removed before the object reaches serialization.
                    ["privateId"] = id, ["privateLogin"] = login });
            }
            // Drain the bounded session result even when it contains the sentinel row.
            if (!reader.NextResult()) throw new InvalidOperationException("Lifecycle mapping unavailable.");
            var mapped = new Dictionary<int, List<object>>();
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                if (!mapped.TryGetValue(id, out var rows)) mapped[id] = rows = new();
                if (rows.Count >= 5) continue;
                rows.Add(new {
                    userTransaction = reader.GetBoolean(1),
                    type = reader.GetInt32(2) switch { 1 => "readWrite", 2 => "readOnly", 3 => "system", 4 => "distributed", _ => "unavailable" },
                    state = reader.GetInt32(3) switch { 0 => "initializing", 1 => "initialized", 2 => "active", 3 => "ended", 4 => "commitInitiated", 5 => "prepared", 6 => "committed", 7 => "rollingBack", 8 => "rolledBack", _ => "unavailable" },
                    databaseAssociation = reader.IsDBNull(4) ? "unavailable" : reader.GetBoolean(4) ? "target" : "other" });
            }
            foreach (Dictionary<string, object?> session in sessions)
            {
                var id = (int)session["privateId"]!;
                var rows = mapped.GetValueOrDefault(id) ?? new();
                // A missing mapping for a positive count is not a zero-transaction claim.
                var count = session["openTransactions"] as int?;
                session["transactionsAvailability"] = count.HasValue && (rows.Count == count || rows.Count == 5 && count > 4) ? "available" : "unavailable";
                session["transactionsTruncated"] = rows.Count > 4;
                session["transactions"] = rows.Take(4).ToArray();
                session.Remove("privateId"); session.Remove("privateLogin");
            }
            _observations.Add(new { phase, availability = "available", errorCategory = "none", truncated, sessions });
        }
        catch {
            if (_observations.Count > before) _observations.RemoveAt(before);
            _observations.Add(new { phase, availability = "unavailable", errorCategory = "metadataUnavailable", truncated = false, sessions = Array.Empty<object>() });
        }
    }

    // Best-effort metadata only. The original guard predicate/count is elsewhere.
    internal void ObserveLifecycle(string phase, SqlConnection master, string database)
    {
        if (!LifecycleEnabled) return;
        try {
            using var command = master.CreateCommand(); command.CommandTimeout = 8;
            // Guard enrichment uses IDs from the authoritative materialized guard.
            // It is a later DMV observation, not an atomic snapshot of transaction state.
            var ids = phase == "Guard" ? _guardIds : _identities.Keys.ToList();
            var terms = new List<string>();
            foreach (var id in ids.Take(48)) { var name = "@s" + terms.Count; command.Parameters.AddWithValue(name, id); terms.Add(name); }
            var filter = terms.Count == 0 ? "1=0" : "s.session_id IN (" + string.Join(",", terms) + ")";
            if (phase != "Guard") filter = "(s.database_id=DB_ID(@name) OR " + filter + ")";
            command.Parameters.AddWithValue("@name", database);
            command.CommandText = @"DECLARE @seen TABLE (id int, login_time datetime, user_process bit, status nvarchar(30),
                has_request bit, request_status nvarchar(30), target bit, request_target bit, open_transactions int, connection_id uniqueidentifier);
                INSERT @seen SELECT TOP(17) s.session_id,s.login_time,s.is_user_process,LOWER(s.status),
                CASE WHEN r.session_id IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END,LOWER(r.status),
                CONVERT(bit,CASE WHEN s.database_id=DB_ID(@name) THEN 1 ELSE 0 END),
                CASE WHEN r.session_id IS NULL THEN NULL ELSE CONVERT(bit,CASE WHEN r.database_id=DB_ID(@name) THEN 1 ELSE 0 END) END,
                s.open_transaction_count,c.connection_id FROM sys.dm_exec_sessions s
                OUTER APPLY (SELECT TOP(1) session_id,status,database_id FROM sys.dm_exec_requests WHERE session_id=s.session_id ORDER BY request_id) r
                OUTER APPLY (SELECT TOP(1) connection_id FROM sys.dm_exec_connections WHERE session_id=s.session_id AND parent_connection_id IS NULL ORDER BY connection_id) c
                WHERE s.session_id<>@@SPID AND " + filter + @" ORDER BY s.session_id;
                SELECT * FROM @seen ORDER BY id;
                SELECT v.id,t.is_user_transaction,t.transaction_type,t.transaction_state,t.target
                FROM @seen v CROSS APPLY (SELECT TOP(5) st.is_user_transaction,a.transaction_type,a.transaction_state,
                    CASE WHEN EXISTS(SELECT 1 FROM sys.dm_tran_database_transactions dt WHERE dt.transaction_id=st.transaction_id AND dt.database_id=DB_ID(@name)) THEN CAST(1 AS bit)
                    WHEN EXISTS(SELECT 1 FROM sys.dm_tran_database_transactions dt WHERE dt.transaction_id=st.transaction_id) THEN CAST(0 AS bit) ELSE NULL END AS target
                    FROM sys.dm_tran_session_transactions st JOIN sys.dm_tran_active_transactions a ON a.transaction_id=st.transaction_id
                    JOIN sys.dm_exec_sessions current_session ON current_session.session_id=v.id AND current_session.login_time=v.login_time
                    WHERE st.session_id=v.id ORDER BY st.transaction_id) t ORDER BY v.id;";
            ObserveLifecycle(phase, () => {
                if (phase == "Guard" && _lifecycleGuardCount > 0 && _guardIds.Count == 0)
                    throw new InvalidOperationException("Guard identities unavailable.");
                return command.ExecuteReader();
            });
        } catch { ObserveLifecycle(phase, () => throw new InvalidOperationException("Lifecycle query unavailable.")); }
    }
    internal const int MaximumGuardSessions = 16;
    internal const int MaximumConnectionRoles = 64;
    private readonly ConcurrentDictionary<Guid, SqlFixtureConnectionRole> _roles = new();
    private volatile bool _roleTrackingUnavailable;
    private object? _guardSessionDetails;

    internal void TrackConnection(DbDataReader reader, SqlFixtureConnectionRole role)
    {
        if (!Enabled) return;
        try
        {
            if (reader.NextResult() && reader.Read() && !reader.IsDBNull(0)) TrackConnection(reader.GetGuid(0), role);
            else _roleTrackingUnavailable = true;
        }
        catch { _roleTrackingUnavailable = true; } // Observation must not affect Open/disposal.
    }
    internal void TrackConnection(Guid connectionId, SqlFixtureConnectionRole role)
    {
        if (!Enabled || connectionId == Guid.Empty) return;
        if (!Enum.IsDefined(role)) { _roleTrackingUnavailable = true; return; }
        lock (_roles)
        {
            if (_roles.Count >= MaximumConnectionRoles && !_roles.ContainsKey(connectionId))
            { _roleTrackingUnavailable = true; return; }
            _roles[connectionId] = role;
        }
    }
    internal string ConnectionRole(Guid? connectionId)
        => !Enabled || connectionId == null || connectionId == Guid.Empty ? "Unavailable"
            : _roles.TryGetValue(connectionId.Value, out var role) ? role.ToString()
            : _roleTrackingUnavailable ? "Unavailable" : "Unmatched";

    internal int InspectGuard(Func<DbDataReader> execute)
    {
        _guardSessionDetails = new { availability = "unavailable", truncated = (bool?)null, sessions = (object[]?)null };
        DbDataReader? reader = null;
        int count;
        try { reader = execute(); count = ReadGuardCount(reader); }
        catch
        {
            try { reader?.Dispose(); } catch { /* Preserve the original query/count exception. */ }
            throw;
        }
        try { reader.Dispose(); }
        catch { _guardSessionDetails = new { availability = "unavailable", truncated = (bool?)null, sessions = (object[]?)null }; }
        return count; // The original nonzero guard executes even if metadata/disposal failed.
    }

    // SQL has already materialized the full matching row set. Metadata failure
    // after reading its count must never bypass the original nonzero-count guard.
    internal int ReadGuardCount(DbDataReader reader)
    {
        if (!reader.Read()) throw new InvalidOperationException("Guard count unavailable.");
        var count = reader.GetInt32(0);
        _lifecycleGuardCount = count;
        _guardSessionDetails = new { availability = "unavailable", truncated = (bool?)null, sessions = (object[]?)null };
        try
        {
            _guardIds.Clear();
            if (!reader.NextResult()) return count;
            var details = new List<object>();
            while (reader.Read())
            {
                if (details.Count >= MaximumGuardSessions) return count;
                if (LifecycleEnabled && reader.FieldCount >= 7) {
                    var id = Convert.ToInt32(reader.GetValue(5)); _guardIds.Add(id);
                    Correlate(id, reader.IsDBNull(6) ? null : reader.GetDateTime(6));
                }
                var status = reader.GetString(1).ToLowerInvariant();
                details.Add(new
                {
                    userProcess = reader.GetBoolean(0),
                    status = status is "running" or "sleeping" or "dormant" or "preconnect" ? status : "unknown",
                    hasActiveRequest = reader.GetBoolean(2),
                    openTransactions = reader.GetInt32(3),
                    fixtureRole = ConnectionRole(reader.IsDBNull(4) ? null : reader.GetGuid(4))
                });
            }
            if (details.Count == Math.Min(count, MaximumGuardSessions))
                _guardSessionDetails = new { availability = "available", truncated = count > MaximumGuardSessions, sessions = details.ToArray() };
        }
        catch { /* Partial/malformed metadata is explicitly unavailable; retain count. */ }
        return count;
    }
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
    {
        _open[connection] = 0; Interlocked.Increment(ref _opened);
    }
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
            ["guardSessionDetails"] = _guardSessionDetails ?? new { availability = "unavailable", truncated = (bool?)null, sessions = (object[]?)null },
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
                if (error != null && operation == "ActiveSessionGuard") ObserveLifecycle("PostRefusal", master, databaseName);
            }
            catch (Exception snapshotError)
            {
                // Secondary diagnostic failure is explicit and cannot suppress the
                // original cleanup exception; DeleteAsync always rethrows it.
                data["snapshotExceptionType"] = snapshotError.GetType().FullName;
                data["snapshotSqlErrors"] = snapshotError is SqlException sqlError ? SqlCodes(sqlError) : null;
            }
        }
        if (LifecycleEnabled) {
            data["lifecycle"] = new { availability = "available", scenario = _scenario.ToString(), bodyCompleted = _bodyCompleted, observations = _observations };
            if (JsonSerializer.Serialize(data).Length > 16384)
                data["lifecycle"] = new { availability = "unavailable", scenario = _scenario.ToString(), bodyCompleted = _bodyCompleted, observations = Array.Empty<object>() };
        }
        try { _write("SQL-CLEANUP-DIAGNOSTIC:" + JsonSerializer.Serialize(data)); }
        finally { _identities.Clear(); _ambiguous.Clear(); _guardIds.Clear(); _roles.Clear(); _observations.Clear(); _phases.Clear(); }
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

internal enum SqlFixtureConnectionRole
{
    SetupMaster, SetupOwnership, OwnershipVerification, OwnedFixture,
    GuardMaster, DiagnosticMaster
}

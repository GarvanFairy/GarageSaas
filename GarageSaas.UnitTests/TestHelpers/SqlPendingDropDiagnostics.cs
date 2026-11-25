using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace GarageSaas.UnitTests.TestHelpers;

// Diagnostic-only, single observation. No target-catalog connection or SQL text.
internal sealed class SqlPendingDropDiagnostics
{
    internal sealed record Observation(string availability, string reason, string request, string status,
        string wait, string blocking, string blockerRequest, int? transactions, string error,
        int[]? sqlErrorNumbers, int? elapsedMilliseconds);
    private sealed record State(bool Pending, Observation? Value);
    private State _state = new(false, null);
    private bool _bodyCompleted;
    private int? _elapsed;
    private bool _capped;
    internal Task Watcher { get; private set; } = Task.CompletedTask;
    internal void BodyCompleted() => _bodyCompleted = true;
    internal static Observation Unavailable(string reason = "NotObserved", string error = "None", int[]? numbers = null)
        => new("Unavailable", reason, "Unavailable", "Unavailable", "Unavailable", "Unavailable", "Unavailable", null, error, numbers, null);
    internal object Evidence => new { availability = "Available", bodyCompleted = _bodyCompleted,
        elapsedMilliseconds = _elapsed, durationCapped = _capped, observation = Volatile.Read(ref _state).Value ?? Unavailable() };

    internal void Execute(Action drop, Func<CancellationToken, Task<Observation>> observe,
        Func<CancellationToken, Task>? trigger = null)
    {
        CancellationTokenSource? finished = null;
        Stopwatch? clock = null;
        try
        {
            clock = Stopwatch.StartNew(); finished = new();
            var token = finished.Token;
            _state = new(true, null);
            Watcher = Task.Run(async () =>
            {
                try
                {
                    // A diagnostic trigger, not a delay/retry of DROP.
                    await (trigger?.Invoke(token) ?? Task.Delay(TimeSpan.FromSeconds(4), token));
                    if (!Volatile.Read(ref _state).Pending) return;
                    using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
                    budget.CancelAfter(TimeSpan.FromSeconds(2));
                    try
                    {
                        var work = observe(budget.Token);
                        ObserveFault(work);
                        var result = await work.WaitAsync(TimeSpan.FromSeconds(2), token);
                        if (!budget.IsCancellationRequested) Publish(result);
                    }
                    finally
                    {
                        // Cancellation belongs only to the observer; callbacks and
                        // driver/socket drain never execute on the DROP thread.
                        try { await budget.CancelAsync(); } catch { }
                    }
                }
                catch (Exception error) { Publish(Failure(error)); }
            });
            ObserveFault(Watcher);
            // Dispose cancellation resources off the original DROP thread.
            var source = finished;
            _ = Watcher.ContinueWith(_ => source.Dispose(), CancellationToken.None,
                TaskContinuationOptions.None, TaskScheduler.Default);
        }
        catch { /* Diagnostic setup cannot prevent the single original DROP. */ }
        try { drop(); }
        finally
        {
            try
            {
                State old;
                do { old = Volatile.Read(ref _state); }
                while (!ReferenceEquals(Interlocked.CompareExchange(ref _state, new(false, old.Value), old), old));
                if (clock != null) { var duration = SqlCleanupDiagnostics.BoundDuration(clock.ElapsedMilliseconds); _elapsed = duration.Milliseconds; _capped = duration.Capped; }
                // Never await the observer or run its cancellation callbacks here.
                if (finished != null) ObserveFault(finished.CancelAsync());
            }
            catch { /* Preserve the original result, exception identity and stack. */ }
        }
    }
    private void Publish(Observation value)
    {
        var old = Volatile.Read(ref _state);
        if (old.Pending) Interlocked.CompareExchange(ref _state, new(true, Sanitize(value)), old);
        // A late observer can never mutate evidence after DROP completed.
    }
    internal static Observation Sanitize(Observation? value)
    {
        if (value == null) return Unavailable("InvalidMetadata");
        bool Is(string item, params string[] allowed) => allowed.Contains(item, StringComparer.Ordinal);
        if (value.availability == "Available")
        {
            if (value.reason != "Captured" || value.request != "DropDatabase" || value.error != "None" || value.sqlErrorNumbers != null ||
                value.elapsedMilliseconds is null or < 0 or > 2000 || value.transactions is < 0 or > 1000000 ||
                !Is(value.status, "Running", "Runnable", "Suspended", "Sleeping", "Background", "Unknown") ||
                !Is(value.wait, "NoneReported", "Lock", "LogIO", "IO", "FileIO", "Latch", "Scheduler", "Unknown") ||
                !Is(value.blocking, "NoneReported", "UserSession", "SystemSession", "OrphanedTransaction", "DeferredRecovery", "InternalOwnerUnknown", "Unknown") ||
                !Is(value.blockerRequest, "DropDatabase", "DatabaseStartup", "Checkpoint", "LazyWriter", "LogWriter", "RecoveryWriter", "Unknown")) return Unavailable("InvalidMetadata");
            return value;
        }
        if (value.availability != "Unavailable" || !Is(value.reason, "NotObserved", "NoMatchingRequest", "CorrelationUnavailable", "InvalidMetadata", "ObserverFailed", "DeadlineOrCancellation") ||
            !Is(value.error, "None", "Sql", "Cancelled", "Other") || value.request != "Unavailable" || value.status != "Unavailable" ||
            value.wait != "Unavailable" || value.blocking != "Unavailable" || value.blockerRequest != "Unavailable" || value.transactions != null || value.elapsedMilliseconds != null ||
            (value.reason == "ObserverFailed") != Is(value.error, "Sql", "Other") || (value.reason == "DeadlineOrCancellation") != (value.error == "Cancelled")) return Unavailable("InvalidMetadata");
        if (value.sqlErrorNumbers != null && (value.error != "Sql" || value.sqlErrorNumbers.Length is < 1 or > 4 ||
            value.sqlErrorNumbers.Distinct().Count() != value.sqlErrorNumbers.Length || value.sqlErrorNumbers.Any(x => !new[] { -2, 0, 53, 233, 229, 297, 300 }.Contains(x)))) return Unavailable("InvalidMetadata");
        return value with { sqlErrorNumbers = value.sqlErrorNumbers?.ToArray() };
    }
    private static void ObserveFault(Task task) => _ = task.ContinueWith(t => { _ = t.Exception; },
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    internal static Observation Failure(Exception error)
    {
        if (error is OperationCanceledException or TimeoutException) return Unavailable("DeadlineOrCancellation", "Cancelled");
        if (error is SqlException sql)
        {
            var allowed = new[] { -2, 0, 53, 233, 229, 297, 300 };
            var numbers = sql.Errors.Cast<SqlError>().Take(16).Select(x => x.Number).Where(allowed.Contains).Distinct().Take(4).ToArray();
            return Unavailable("ObserverFailed", "Sql", numbers.Length == 0 ? null : numbers);
        }
        return Unavailable("ObserverFailed", "Other");
    }

    internal static string Status(string? value) => value switch {
        "running" => "Running", "runnable" => "Runnable", "suspended" => "Suspended",
        "sleeping" => "Sleeping", "background" => "Background", _ => "Unknown" };
    internal static string Wait(string? value) => value switch {
        null or "" => "NoneReported",
        "LCK_M_S" or "LCK_M_U" or "LCK_M_X" or "LCK_M_IS" or "LCK_M_IX" or "LCK_M_SIX" or "LCK_M_SCH_S" or "LCK_M_SCH_M" => "Lock",
        "WRITELOG" => "LogIO",
        "PAGEIOLATCH_SH" or "PAGEIOLATCH_EX" or "IO_COMPLETION" or "ASYNC_IO_COMPLETION" => "IO",
        "PREEMPTIVE_OS_DELETEFILE" or "PREEMPTIVE_OS_FLUSHFILEBUFFERS" or "PREEMPTIVE_OS_WRITEFILE" or "PREEMPTIVE_OS_CREATEFILE" => "FileIO",
        "PAGELATCH_SH" or "PAGELATCH_EX" or "LATCH_SH" or "LATCH_EX" => "Latch",
        "SOS_SCHEDULER_YIELD" => "Scheduler", _ => "Unknown" };
    internal static string Request(string? value) => value switch {
        "DROP DATABASE" => "DropDatabase", "DB STARTUP" => "DatabaseStartup", "CHECKPOINT" => "Checkpoint",
        "LAZY WRITER" => "LazyWriter", "LOG WRITER" => "LogWriter", "RECOVERY WRITER" => "RecoveryWriter", _ => "Unknown" };
    internal static string Blocking(int value, bool? user) => value switch {
        0 => "NoneReported", -2 => "OrphanedTransaction", -3 => "DeferredRecovery",
        -4 or -5 => "InternalOwnerUnknown", > 0 => user switch { true => "UserSession", false => "SystemSession", _ => "Unknown" }, _ => "Unknown" };

    // Inputs originate only from the already identity-verified cleanup master.
    // No database name/SQL text/principal/session identifier is ever serialized.
    internal static string ObserverConnection(string validatedPipe)
    {
        var pipePattern = "\\A" + Regex.Escape(@"np:\\.\pipe\") + @"LOCALDB#[A-Za-z0-9]+" + Regex.Escape(@"\tsql\query") + "\\z";
        if (!Regex.IsMatch(validatedPipe, pipePattern, RegexOptions.IgnoreCase)) throw new InvalidOperationException("Observer target unavailable.");
        return new SqlConnectionStringBuilder { DataSource = validatedPipe, InitialCatalog = "master", IntegratedSecurity = true,
            TrustServerCertificate = true, Pooling = false, ConnectTimeout = 1, ConnectRetryCount = 0 }.ConnectionString;
    }
    internal static Func<CancellationToken, Task<Observation>> Probe(SqlConnection master, Guid serverConnectionId)
    {
        var pipePattern = "\\A" + Regex.Escape(@"np:\\.\pipe\") + @"LOCALDB#[A-Za-z0-9]+" + Regex.Escape(@"\tsql\query") + "\\z";
        if (master.State != ConnectionState.Open || master.Database != "master" || serverConnectionId == Guid.Empty ||
            master.ServerProcessId < 1 || !Regex.IsMatch(master.DataSource, pipePattern, RegexOptions.IgnoreCase))
            return _ => Task.FromResult(Unavailable("CorrelationUnavailable"));
        var id = master.ServerProcessId;
        var connectionString = ObserverConnection(master.DataSource);
        return async token =>
        {
            var clock = Stopwatch.StartNew();
            using var observer = new SqlConnection(connectionString);
            await observer.OpenAsync(token);
            if (observer.Database != "master") return Unavailable("CorrelationUnavailable");
            using var command = observer.CreateCommand(); command.CommandTimeout = 1;
            command.CommandText = @"SELECT TOP(2) r.command,r.status,r.wait_type,CONVERT(int,r.blocking_session_id),
                b.is_user_process,br.command,r.open_transaction_count
                FROM sys.dm_exec_requests r
                JOIN sys.dm_exec_connections c ON c.session_id=r.session_id AND c.connection_id=@connection
                LEFT JOIN sys.dm_exec_sessions b ON b.session_id=r.blocking_session_id
                OUTER APPLY (SELECT TOP(1) command FROM sys.dm_exec_requests br
                    WHERE br.session_id=r.blocking_session_id ORDER BY br.request_id) br
                WHERE r.session_id=@session";
            command.Parameters.Add("@connection", SqlDbType.UniqueIdentifier).Value = serverConnectionId;
            command.Parameters.Add("@session", SqlDbType.Int).Value = id;
            using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token) || Request(reader.IsDBNull(0) ? null : reader.GetString(0)) != "DropDatabase")
                return Unavailable("NoMatchingRequest");
            var transactions = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);
            if (transactions is < 0 or > 1000000) return Unavailable("InvalidMetadata");
            var elapsed = clock.ElapsedMilliseconds;
            if (elapsed > 2000) return Unavailable("DeadlineOrCancellation", "Cancelled");
            var result = new Observation("Available", "Captured", "DropDatabase", Status(reader.IsDBNull(1) ? null : reader.GetString(1)),
                Wait(reader.IsDBNull(2) ? null : reader.GetString(2)), Blocking(reader.GetInt32(3), reader.IsDBNull(4) ? null : reader.GetBoolean(4)),
                Request(reader.IsDBNull(5) ? null : reader.GetString(5)), transactions, "None", null, (int)elapsed);
            if (await reader.ReadAsync(token)) return Unavailable("InvalidMetadata");
            return result;
        };
    }
}

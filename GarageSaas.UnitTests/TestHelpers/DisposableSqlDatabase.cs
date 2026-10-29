using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.TestHelpers;

// Two fixed local targets only. Shared-server use needs its own additional opt-in.
// No remote servers, aliases, application configuration or caller-supplied catalogs.
internal sealed class DisposableSqlDatabase
{
    internal const string Instance = "GarageSaasIntegrationTests";
    internal const string Server = @"(localdb)\GarageSaasIntegrationTests";
    internal const string SharedServer = "GARVAN_ASUS";
    internal const string SharedServerOptIn = "GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER";
    private const string DatabasePattern = @"\AGarageSaas_(Registration|InvoiceAtomic|QuoteConversion|QuoteAtomic|QuoteCoordination)_Test_[a-f0-9]{32}\z";
    private static readonly string PipePattern = Regex.Escape(@"np:\\.\pipe\") + @"(LOCALDB#[A-Za-z0-9]+)" + Regex.Escape(@"\tsql\query");
    private readonly string _dataSource;
    private readonly bool _sharedServer;
    private readonly string _instanceIdentity;
    private readonly string _owner = Guid.NewGuid().ToString("N");
    private readonly SqlTestWorkers _workers = new();
    private readonly SqlCleanupDiagnostics _cleanupDiagnostics = new();
    private bool _created;
    private bool _marked;
    internal string DatabaseName { get; }
    internal string ConnectionString => Connection(DatabaseName);

    internal static bool Enabled(string flag, Func<string, string?> environment) => environment(flag) == "1";
    internal static void ValidateServer(string? server, bool sharedServerOptIn = false)
    {
        if (string.Equals(server, Server, StringComparison.OrdinalIgnoreCase)) return;
        if (sharedServerOptIn && string.Equals(server, SharedServer, StringComparison.OrdinalIgnoreCase)) return;
        throw new InvalidOperationException($"Only {Server}, or {SharedServer} with separate explicit opt-in, is allowed.");
    }

    internal static void ValidateSharedIdentity(SqlServerIdentity identity, string localMachine)
    {
        bool Approved(string value) => string.Equals(value, SharedServer, StringComparison.OrdinalIgnoreCase);
        if (!Approved(localMachine) || !Approved(identity.Machine) || !Approved(identity.PhysicalMachine) ||
            !Approved(identity.Server) || !Approved(identity.RegisteredServer) || identity.Instance != null ||
            identity.IsLocalDb || identity.IsClustered || !string.Equals(identity.Transport, "Shared memory", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved SQL identity is not the approved local default instance over shared memory.");
    }

    internal static void ValidateCatalog(string catalog, bool allowMaster = false)
    {
        // Never accept application/system databases as a test catalog, regardless
        // of opt-in, case, marker contents or permissions. Master is metadata-only.
        if (allowMaster && string.Equals(catalog, "master", StringComparison.Ordinal)) return;
        if (string.Equals(catalog, "Garagesaas", StringComparison.OrdinalIgnoreCase) || !Regex.IsMatch(catalog, DatabasePattern))
            throw new InvalidOperationException("Only a generated test database catalog is permitted.");
    }

    internal static void ValidateSharedMetadata(bool canInspectDefinitions, int serverTriggers, int notifications, int modelObjects)
    {
        if (!canInspectDefinitions || serverTriggers != 0 || notifications != 0 || modelObjects != 0)
            throw new InvalidOperationException("Shared SQL instance has unverified metadata, DDL hooks or model customizations; refusing database operations.");
    }
    internal static void ValidateIdentity(string pipe, string machine, string instance, bool localDb)
    {
        var match = Regex.Match(pipe, "^" + PipePattern + "$", RegexOptions.IgnoreCase);
        if (!localDb || !match.Success || !string.Equals(machine, Environment.MachineName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(instance, match.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved SQL instance identity does not match the dedicated local pipe.");
    }

    internal static void ValidateOwnership(bool created, bool marked, string expectedDatabase, string actualDatabase, string expectedOwner, string? actualOwner)
    {
        if (!created || !marked ||
            !Regex.IsMatch(expectedDatabase, DatabasePattern) ||
            !string.Equals(expectedDatabase, actualDatabase, StringComparison.Ordinal) ||
            !string.Equals(expectedOwner, actualOwner, StringComparison.Ordinal) || string.IsNullOrEmpty(expectedOwner))
            throw new InvalidOperationException("Database ownership was not confirmed; refusing deletion.");
    }

    internal static void ValidateDatabaseInventory(IEnumerable<string> userDatabases)
    {
        if (userDatabases.Any(name => !Regex.IsMatch(name, DatabasePattern)))
            throw new InvalidOperationException("The dedicated instance contains a non-test database; refusing database operations.");
    }

    private void VerifyTarget(SqlConnection master)
    {
        using var command = master.CreateCommand(); command.CommandTimeout = 8;
        // Complete catalog/session visibility is required for collision/cleanup
        // checks. SQL 2022+ uses VIEW SERVER PERFORMANCE STATE for this DMV.
        command.CommandText = @"SELECT HAS_PERMS_BY_NAME('master','DATABASE','CREATE DATABASE'),
            HAS_PERMS_BY_NAME(NULL,NULL,'VIEW ANY DATABASE'),
            CASE WHEN CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) >= 16
                THEN HAS_PERMS_BY_NAME(NULL,NULL,'VIEW SERVER PERFORMANCE STATE')
                ELSE HAS_PERMS_BY_NAME(NULL,NULL,'VIEW SERVER STATE') END";
        using (var permissions = command.ExecuteReader())
        {
            if (!permissions.Read() || Enumerable.Range(0, 3).Any(i => permissions.IsDBNull(i) || permissions.GetInt32(i) != 1))
                throw new InvalidOperationException("SQL test create/catalog/session permissions are insufficient.");
        }
        // The separately opted-in shared server may contain application databases;
        // they are never opened. Dedicated LocalDB retains its stricter inventory rule.
        if (_sharedServer)
        {
            // Server DDL hooks or objects inherited from model could cause effects
            // outside our test catalogs. Do not execute them or silently ignore them.
            command.CommandText = @"SELECT HAS_PERMS_BY_NAME(NULL,NULL,'VIEW ANY DEFINITION'),
                (SELECT COUNT(*) FROM sys.server_triggers WHERE is_disabled=0),
                (SELECT COUNT(*) FROM sys.server_event_notifications),
                (SELECT COUNT(*) FROM model.sys.objects WHERE is_ms_shipped=0)";
            using var metadata = command.ExecuteReader();
            if (!metadata.Read()) throw new InvalidOperationException("Shared SQL metadata unavailable.");
            ValidateSharedMetadata(!metadata.IsDBNull(0) && metadata.GetInt32(0) == 1,
                metadata.GetInt32(1), metadata.GetInt32(2), metadata.GetInt32(3));
            return;
        }
        command.CommandText = "SELECT name FROM sys.databases WHERE database_id > 4";
        using var reader = command.ExecuteReader(); var names = new List<string>();
        while (reader.Read()) names.Add(reader.GetString(0));
        ValidateDatabaseInventory(names);
    }

    internal static DisposableSqlDatabase Start(string flag, string category)
    {
        // Do not resolve utilities, open a connection or inspect SQL when not opted in.
        if (!Enabled(flag, Environment.GetEnvironmentVariable)) Assert.Inconclusive($"SQL fixture disabled. Requires {flag}=1.");
        var server = Environment.GetEnvironmentVariable("GARAGESAAS_TEST_SQLSERVER");
        ValidateServer(server, Enabled(SharedServerOptIn, Environment.GetEnvironmentVariable));
        if (!Regex.IsMatch(category, @"\A(Registration|InvoiceAtomic|QuoteConversion|QuoteAtomic|QuoteCoordination)\z"))
            throw new ArgumentException("Unknown SQL fixture category.");
        var shared = string.Equals(server, SharedServer, StringComparison.OrdinalIgnoreCase);
        // Force shared memory: never resolve GARVAN_ASUS via DNS or a client alias.
        return new DisposableSqlDatabase(category, shared ? "lpc:" + SharedServer : ResolvePipe(), shared);
    }

    private DisposableSqlDatabase(string category, string dataSource, bool shared)
    {
        _dataSource = dataSource;
        _sharedServer = shared;
        _instanceIdentity = Regex.Match(dataSource, @"LOCALDB#[A-Za-z0-9]+", RegexOptions.IgnoreCase).Value;
        DatabaseName = $"GarageSaas_{category}_Test_{Guid.NewGuid():N}";
    }

    internal DbContextOptions<SignupContext> Options()
    {
        var builder = new DbContextOptionsBuilder<SignupContext>()
            .UseSqlServer(ConnectionString, sql => sql.CommandTimeout(8));
        if (_cleanupDiagnostics.Enabled) builder.AddInterceptors(_cleanupDiagnostics);
        return builder.Options;
    }

    internal void Create()
    {
        ValidateCatalog(DatabaseName);
        using (var master = Open("master", SqlFixtureConnectionRole.SetupMaster))
        {
            VerifyTarget(master);
            using var exists = master.CreateCommand(); exists.CommandTimeout = 8;
            exists.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name=@name";
            exists.Parameters.AddWithValue("@name", DatabaseName);
            if (Convert.ToInt32(exists.ExecuteScalar()) != 0) throw new InvalidOperationException("Refusing to reuse an existing database.");
            // CREATE (not EnsureCreated) resolves the existence-check race: collision
            // fails, and _created remains false. The identifier is generated here.
            using var create = master.CreateCommand(); create.CommandTimeout = 30;
            create.CommandText = $"CREATE DATABASE [{DatabaseName}]";
            create.ExecuteNonQuery(); _created = true;
        }
        using (var database = Open(DatabaseName, SqlFixtureConnectionRole.SetupOwnership))
        {
            using var marker = database.CreateCommand(); marker.CommandTimeout = 8;
            marker.CommandText = "EXEC sys.sp_addextendedproperty @name=N'GarageSaasTestOwner', @value=@owner";
            marker.Parameters.AddWithValue("@owner", _owner); marker.ExecuteNonQuery(); _marked = true;
        }
        using var db = new SignupContext(Options());
        // Existing, exclusively created EMPTY database: creates current model tables.
        db.Database.EnsureCreated();
    }

    internal T Track<T>(T task) where T : Task => _workers.Track(task);
    internal void RegisterCustomerDiagnostics() { if (!_sharedServer) _cleanupDiagnostics.RegisterCustomerDiagnostics(); }
    internal T SeedStep<T>(SqlCleanupDiagnostics.SeedGraph graph, SqlCleanupDiagnostics.SeedStage stage, Func<T> work) => _cleanupDiagnostics.SeedStep(graph, stage, work);
    internal void SeedStep(SqlCleanupDiagnostics.SeedGraph graph, SqlCleanupDiagnostics.SeedStage stage, Action work) => _cleanupDiagnostics.SeedStep(graph, stage, work);
    internal IDisposable SeedDisposal(IDisposable resource) => _cleanupDiagnostics.SeedDisposal(resource);
    internal void RegisterScenario(SqlCleanupDiagnostics.Scenario scenario) { if (!_sharedServer) _cleanupDiagnostics.RegisterScenario(scenario); }
    internal void BodyCompleted() => _cleanupDiagnostics.BodyCompleted();
    private void ObserveLifecycle(string phase)
    {
        if (!_cleanupDiagnostics.LifecycleEnabled || _sharedServer || !_created || !_marked) return;
        try { using var master = Open("master", SqlFixtureConnectionRole.DiagnosticMaster); _cleanupDiagnostics.ObserveLifecycle(phase, master, DatabaseName); }
        catch { _cleanupDiagnostics.ObserveLifecycle(phase, () => throw new InvalidOperationException("Lifecycle connection unavailable.")); }
    }
    internal Task DrainAsync() => _workers.DrainAsync(TimeSpan.FromSeconds(30));

    // Experiments may execute DDL only through a freshly verified connection to
    // this object's successfully created and marked database. Never adopts a DB.
    internal SqlConnection OpenOwnedConnection()
    {
        if (!_created || !_marked) throw new InvalidOperationException("Database creation/ownership was not confirmed.");
        ValidateCatalog(DatabaseName);
        var database = Open(DatabaseName, SqlFixtureConnectionRole.OwnedFixture);
        try
        {
            using var command = database.CreateCommand(); command.CommandTimeout = 8;
            command.CommandText = "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'GarageSaasTestOwner'";
            ValidateOwnership(_created, _marked, DatabaseName, database.Database, _owner, command.ExecuteScalar() as string);
            return database;
        }
        catch { database.Dispose(); throw; }
    }

    internal async Task DeleteAsync()
    {
        var operation = "DrainWorkers";
        int? guardSessions = null;
        var ownershipVerified = false;
        await SqlCleanupDiagnostics.RunCleanupAsync(async () =>
        {
            await DrainAsync();
            operation = "OwnershipPrecondition";
            if (!_created || !_marked) throw new InvalidOperationException("Database creation/ownership was not confirmed; leaving it untouched.");
            ValidateCatalog(DatabaseName);
            ObserveLifecycle("PreOwnership");
            operation = "OpenOwnedDatabase";
            var database = Open(DatabaseName, SqlFixtureConnectionRole.OwnershipVerification);
            using (var databaseLifetime = _cleanupDiagnostics.OpenDisposal(_cleanupDiagnostics.CustomerDiagnosticsEnabled,
                SqlCleanupDiagnostics.OpenStage.ConnectionDispose, database))
            {
                operation = "VerifyOwnershipMarker";
                using var command = database.CreateCommand(); command.CommandTimeout = 8;
                command.CommandText = "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'GarageSaasTestOwner'";
                ValidateOwnership(_created, _marked, DatabaseName, database.Database, _owner, command.ExecuteScalar() as string);
                ownershipVerified = true;
            }
            operation = "OpenMaster";
            using (var master = Open("master", SqlFixtureConnectionRole.GuardMaster))
            {
                operation = "VerifyTarget";
                VerifyTarget(master);
                // Pooling is disabled. Any remaining session means cleanup is unsafe;
                // never forcibly disconnect it using SINGLE_USER/ROLLBACK IMMEDIATE.
                using var sessions = master.CreateCommand(); sessions.CommandTimeout = 8;
                sessions.CommandText = "SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id=DB_ID(@name) AND session_id<>@@SPID";
                sessions.Parameters.AddWithValue("@name", DatabaseName);
                operation = "ActiveSessionGuard";
                if (_cleanupDiagnostics.Enabled)
                {
                    // Same predicate, including internal/idle sessions. One DMV
                    // observation materializes count and metadata together. Only
                    // the returned metadata is capped; the guard count is not.
                    sessions.CommandText = @"DECLARE @observed TABLE
                        (session_id smallint, user_process bit, status nvarchar(30), active_request bit,
                         open_transactions int, connection_id uniqueidentifier, login_time datetime);
                        INSERT @observed
                        SELECT s.session_id, s.is_user_process, s.status,
                            CONVERT(bit, CASE WHEN EXISTS(SELECT 1 FROM sys.dm_exec_requests r WHERE r.session_id=s.session_id) THEN 1 ELSE 0 END),
                            s.open_transaction_count, c.connection_id, s.login_time
                        FROM sys.dm_exec_sessions s
                        OUTER APPLY (SELECT TOP(1) connection_id FROM sys.dm_exec_connections c
                            WHERE c.session_id=s.session_id AND c.parent_connection_id IS NULL ORDER BY c.connection_id) c
                        WHERE s.database_id=DB_ID(@name) AND s.session_id<>@@SPID;
                        SELECT COUNT(*) FROM @observed;
                        SELECT TOP(16) user_process, status, active_request, open_transactions, connection_id, session_id, login_time
                        FROM @observed ORDER BY session_id;";
                    guardSessions = _cleanupDiagnostics.InspectGuard(() => sessions.ExecuteReader());
                    _cleanupDiagnostics.ObserveLifecycle("Guard", master, DatabaseName);
                }
                else guardSessions = Convert.ToInt32(sessions.ExecuteScalar());
                if (guardSessions != 0) throw new InvalidOperationException("Database still has active sessions; refusing deletion.");
                // EF EnsureDeleted may force SINGLE_USER/ROLLBACK IMMEDIATE. Plain
                // DROP instead fails/times out if a session appears after our check.
                using var drop = master.CreateCommand(); drop.CommandTimeout = 8;
                drop.CommandText = $"DROP DATABASE [{DatabaseName}]";
                operation = "DropOwnedDatabase";
                drop.ExecuteNonQuery(); _created = false;
            }
        }, error => {
            _cleanupDiagnostics.Record(error == null ? "Completed" : operation, error,
                _created, _marked, !_sharedServer, ownershipVerified, guardSessions,
                DatabaseName, _workers, () => Open("master", SqlFixtureConnectionRole.DiagnosticMaster));
        });
    }
    private string Connection(string catalog)
    {
        ValidateCatalog(catalog, allowMaster: true);
        return new SqlConnectionStringBuilder
        {
            DataSource = _dataSource, InitialCatalog = catalog, IntegratedSecurity = true,
            Pooling = false, ConnectTimeout = 5, TrustServerCertificate = true,
            ApplicationName = "GarageSaas disposable SQL tests"
        }.ConnectionString;
    }

    private SqlConnection Open(string catalog, SqlFixtureConnectionRole role)
    {
        var observed = _cleanupDiagnostics.CustomerDiagnosticsEnabled && role == SqlFixtureConnectionRole.OwnershipVerification;
        T Step<T>(SqlCleanupDiagnostics.OpenStage stage, Func<T> work) => observed ? _cleanupDiagnostics.OpenStep(stage, work) : work();
        var connection = new SqlConnection(Connection(catalog));
        try
        {
            Step(SqlCleanupDiagnostics.OpenStage.ConnectionOpen, () => { connection.Open(); return true; });
            var command = Step(SqlCleanupDiagnostics.OpenStage.IdentityQuerySetup, connection.CreateCommand);
            using var commandLifetime = _cleanupDiagnostics.OpenDisposal(observed, SqlCleanupDiagnostics.OpenStage.CommandDispose, command);
            Step(SqlCleanupDiagnostics.OpenStage.IdentityQuerySetup, () => {
            command.CommandTimeout = 8;
            command.CommandText = @"SELECT CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')),
                CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')), CONVERT(int,SERVERPROPERTY('IsLocalDB')), DB_NAME(),
                CONVERT(nvarchar(128),SERVERPROPERTY('ComputerNamePhysicalNetBIOS')),
                CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')), CONVERT(nvarchar(128),@@SERVERNAME),
                CONVERT(int,SERVERPROPERTY('IsClustered')), CONVERT(nvarchar(128),CONNECTIONPROPERTY('net_transport'))";
            if (_cleanupDiagnostics.Enabled)
                command.CommandText += @";
                    BEGIN TRY
                        SELECT TOP(1) connection_id FROM sys.dm_exec_connections
                        WHERE session_id=@@SPID AND parent_connection_id IS NULL ORDER BY connection_id;
                    END TRY
                    BEGIN CATCH SELECT CAST(NULL AS uniqueidentifier); END CATCH";
            return true;
            });
            var reader = Step(SqlCleanupDiagnostics.OpenStage.IdentityQueryExecute, command.ExecuteReader);
            using var readerLifetime = _cleanupDiagnostics.OpenDisposal(observed, SqlCleanupDiagnostics.OpenStage.ReaderDispose, reader);
            Step(SqlCleanupDiagnostics.OpenStage.IdentityQueryRead, () => {
            if (!reader.Read()) throw new InvalidOperationException("SQL instance identity unavailable.");
            if (_sharedServer)
                ValidateSharedIdentity(new SqlServerIdentity(reader.GetString(0), reader.GetString(4),
                    reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(5), reader.GetString(6),
                    reader.GetInt32(2) == 1, reader.GetInt32(7) == 1, reader.GetString(8)), Environment.MachineName);
            else
            {
                ValidateIdentity(_dataSource, reader.GetString(0), reader.GetString(1), reader.GetInt32(2) == 1);
                if (!string.Equals(reader.GetString(1), _instanceIdentity, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Resolved LocalDB instance mismatch.");
            }
            if (!string.Equals(reader.GetString(3), catalog, StringComparison.Ordinal))
                throw new InvalidOperationException("Resolved server/catalog mismatch.");
            return true;
            });
            Step(SqlCleanupDiagnostics.OpenStage.RoleQueryRead, () => { _cleanupDiagnostics.TrackConnection(reader, role); return true; });
            return Step(SqlCleanupDiagnostics.OpenStage.Completed, () => connection);
        }
        catch { Step(SqlCleanupDiagnostics.OpenStage.ConnectionDispose, () => { connection.Dispose(); return true; }); throw; }
    }

    private static string ResolvePipe()
    {
        // Use only an installed Microsoft utility under Program Files, never PATH.
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft SQL Server");
        var executable = Directory.GetDirectories(root).OrderByDescending(x => x, StringComparer.Ordinal)
            .Select(x => Path.Combine(x, "Tools", "Binn", "SqlLocalDB.exe")).FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("SQL LocalDB utility is not installed.");
        using var process = new Process { StartInfo = new ProcessStartInfo(executable)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        process.StartInfo.ArgumentList.Add("info"); process.StartInfo.ArgumentList.Add(Instance);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(5000)) { process.Kill(true); throw new TimeoutException("LocalDB identity discovery timed out."); }
        if (process.ExitCode != 0) throw new InvalidOperationException("Start the dedicated LocalDB instance before running SQL tests.");
        var pipes = Regex.Matches(output.GetAwaiter().GetResult(), PipePattern, RegexOptions.IgnoreCase);
        _ = error.GetAwaiter().GetResult();
        if (pipes.Count != 1) throw new InvalidOperationException("Dedicated LocalDB pipe identity is unavailable or ambiguous.");
        return pipes[0].Value;
    }
}

internal sealed record SqlServerIdentity(string Machine, string PhysicalMachine, string? Instance,
    string Server, string RegisteredServer, bool IsLocalDb, bool IsClustered, string Transport);

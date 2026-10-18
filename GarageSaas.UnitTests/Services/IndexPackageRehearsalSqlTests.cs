using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using GarageSaas.Services;
using GarageSaas.Services.Models;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// Opt-in only. Every case creates a separate owned DB, including fault cases.
// Production SQL files are never submitted to the application catalog. Only the
// exact catalog guard is replaced IN MEMORY with a stricter harness-owner guard.
[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlIndexPackageRehearsal")]
public class IndexPackageRehearsalSqlTests
{
    private const string Flag = "GARAGESAAS_RUN_INDEX_PACKAGE_REHEARSAL_SQL_TESTS";
    private const string Package = "GarageSaas.ThreeAssociationIndexes.v1";
    private readonly List<object> _events = new();
    private readonly Dictionary<string, object> _report = new();
    private DisposableSqlDatabase _database = null!;
    private Guid _deployment;
    private static bool _cleanupUncertain;
    private string _directory = null!, _artifact = null!;
    public TestContext TestContext { get; set; } = null!;
    private static readonly string[] Names =
    {
        "IX_VehicleInvoice_WorkQuoteId", "IX_InvoiceWorkQuote_WorkQuoteId_InvoiceId", "IX_InvoiceWorkQuote_InvoiceId_WorkQuoteId"
    };

    [TestMethod]
    public Task SmokeRuntimeBinding() => Rehearse("clean-and-verify");

    [DataTestMethod]
    [DataRow("preview")]
    [DataRow("clean-and-verify")]
    [DataRow("same-guid-rerun")]
    [DataRow("partial-and-resume")]
    [DataRow("preexisting-equivalents")]
    [DataRow("incompatible-name")]
    [DataRow("incompatible-overlap")]
    [DataRow("wrong-target")]
    [DataRow("missing-package-marker")]
    [DataRow("missing-deployment-marker")]
    [DataRow("different-deployment-marker")]
    [DataRow("changed-owned-definition")]
    [DataRow("repeated-rollback")]
    [DataRow("create-before-marker-failure")]
    [DataRow("different-guid-rerun")]
    [DataRow("wrong-column-type")]
    [DataRow("approval-gates")]
    [DataRow("offline-build-blocking")]
    [DataRow("business-regression")]
    public async Task Rehearse(string scenario)
    {
        if (_cleanupUncertain) Assert.Inconclusive("An earlier cleanup/creation outcome is uncertain. Stop; inspect it before any more SQL runs.");
        _database = DisposableSqlDatabase.Start(Flag, "InvoiceAtomic");
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The approved local rehearsal requires Windows."); return; }
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        Assert.AreEqual(@"GARVAN_ASUS\ggall", identity.Name, true);
        _deployment = Guid.NewGuid();
        _directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "database", "deployments", "three-association-indexes"));
        _artifact = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestResults", "IndexPackageRehearsal", _database.DatabaseName));
        Directory.CreateDirectory(_artifact);
        _report["scenario"] = scenario; _report["database"] = _database.DatabaseName;
        _report["windowsIdentity"] = identity.Name;
        _report["deploymentId"] = _deployment; _report["events"] = _events;
        var created = false;
        try
        {
            VerifyHashes();
            _database.Create(); created = true;
            using (var connection = _database.OpenOwnedConnection())
            {
                _report["identity"] = Rows(connection, "SELECT ORIGINAL_LOGIN(),SERVERPROPERTY('ServerName'),CONNECTIONPROPERTY('net_transport'),DB_NAME()");
                new AssociationIndexExperimentSqlTests().Align(connection);
                var deployed = AssociationIndexExperimentSqlTests.Schema(connection, true);
                var aligned = AssociationIndexExperimentSqlTests.Schema(connection, false);
                var differing = aligned.Except(deployed).Concat(deployed.Except(aligned)).ToArray();
                Assert.AreEqual(6, differing.Length);
                foreach (var column in new[] { "BusinessRegistrationNumber", "VatNumber", "LogoImage" })
                {
                    var prefix = "COLUMN|dbo|GarageBusiness|" + column + "|";
                    var a = aligned.Single(x => x.StartsWith(prefix, StringComparison.Ordinal)).Split('|');
                    var d = deployed.Single(x => x.StartsWith(prefix, StringComparison.Ordinal)).Split('|');
                    Assert.AreEqual("510", a[5]); Assert.AreEqual(column == "LogoImage" ? "1000" : "100", d[5]);
                    a[5] = d[5]; CollectionAssert.AreEqual(d, a);
                }
                _report["schemaResiduals"] = differing;
                _report["schemaClassification"] = "Only three approved GarageBusiness string widths differ; package DDL concerns native int columns/indexes on VehicleInvoice/InvoiceWorkQuote. Seed values fit both. Not complete schema parity.";
            }
            switch (scenario)
            {
                case "preview":
                    var before = Inventory();
                    Run("00-preflight.sql"); Run("01-deploy.sql", execute: false); Run("02-rollback.sql", execute: false);
                    Assert.AreEqual(before, Inventory()); break;
                case "clean-and-verify":
                    Deploy(); AssertOwned(3); VerifyDefinitions(); Run("03-verify.sql");
                    Rollback(); AssertOwned(0); break;
                case "same-guid-rerun":
                    Deploy(); var inventory = Inventory(); Deploy(); Assert.AreEqual(inventory, Inventory()); AssertOwned(3); break;
                case "partial-and-resume":
                    Error(() => Run("01-deploy.sql", true, inject: "before-second"), 51991);
                    AssertOwned(1); Deploy(); AssertOwned(3); VerifyDefinitions(); Rollback(); AssertOwned(0); break;
                case "preexisting-equivalents":
                    Sql("CREATE INDEX IX_VehicleInvoice_WorkQuoteId ON dbo.VehicleInvoice(WorkQuoteId) INCLUDE(GarageBusinessId);");
                    Sql("CREATE INDEX ExistingJoinByQuote ON dbo.InvoiceWorkQuote(WorkQuoteId,InvoiceId) INCLUDE(GarageBusinessCustomerId,CreatedDate);");
                    Deploy(); AssertOwned(1); Run("03-verify.sql"); Rollback(); Rollback();
                    Assert.IsTrue(Exists(Names[0])); Assert.IsTrue(Exists("ExistingJoinByQuote")); Assert.IsFalse(Exists(Names[2]));
                    Assert.AreEqual(0, OwnerCount()); break;
                case "incompatible-name":
                    Sql("CREATE INDEX IX_InvoiceWorkQuote_InvoiceId_WorkQuoteId ON dbo.InvoiceWorkQuote(InvoiceId);");
                    var incompatible = Inventory(); Error(Deploy, 51000); Assert.AreEqual(incompatible, Inventory()); break;
                case "incompatible-overlap":
                    Sql("CREATE INDEX ExistingReverse ON dbo.VehicleInvoice(WorkQuoteId DESC) INCLUDE(GarageBusinessId);");
                    var overlap = Inventory(); Error(Deploy, 51000); Assert.AreEqual(overlap, Inventory()); break;
                case "wrong-target":
                    Error(() => Run("01-deploy.sql", true, originalGuard: true), 51000);
                    Error(() => Run("01-deploy.sql", true, wrongExpected: true), 51000); AssertOwned(0); break;
                case "missing-package-marker":
                    Deploy(); Sql("EXEC sys.sp_dropextendedproperty @name=N'GarageSaas.IndexPackage',@level0type=N'SCHEMA',@level0name=N'dbo',@level1type=N'TABLE',@level1name=N'VehicleInvoice',@level2type=N'INDEX',@level2name=N'IX_VehicleInvoice_WorkQuoteId';");
                    Rollback(); Assert.IsTrue(Exists(Names[0])); Assert.IsFalse(Exists(Names[1])); Assert.IsFalse(Exists(Names[2])); break;
                case "missing-deployment-marker":
                    Deploy(); Sql("EXEC sys.sp_dropextendedproperty @name=N'GarageSaas.DeploymentId',@level0type=N'SCHEMA',@level0name=N'dbo',@level1type=N'TABLE',@level1name=N'VehicleInvoice',@level2type=N'INDEX',@level2name=N'IX_VehicleInvoice_WorkQuoteId';");
                    var missing = Inventory(); Error(Rollback, 51000); Assert.AreEqual(missing, Inventory()); break;
                case "different-deployment-marker":
                    Deploy(); Sql($"EXEC sys.sp_updateextendedproperty @name=N'GarageSaas.DeploymentId',@value=N'{Guid.NewGuid():D}',@level0type=N'SCHEMA',@level0name=N'dbo',@level1type=N'TABLE',@level1name=N'VehicleInvoice',@level2type=N'INDEX',@level2name=N'IX_VehicleInvoice_WorkQuoteId';");
                    Rollback(); Assert.IsTrue(Exists(Names[0])); Assert.IsFalse(Exists(Names[1])); Assert.IsFalse(Exists(Names[2])); break;
                case "changed-owned-definition":
                    Deploy(); Sql("ALTER INDEX IX_VehicleInvoice_WorkQuoteId ON dbo.VehicleInvoice DISABLE;");
                    var disabled = Inventory(); Error(Rollback, 51000); Assert.AreEqual(disabled, Inventory()); break;
                case "repeated-rollback":
                    Sql("CREATE INDEX UnrelatedCustomer ON dbo.VehicleInvoice(CustomerId);");
                    Deploy(); Rollback(); var once = Inventory(); Rollback(); Assert.AreEqual(once, Inventory()); Assert.IsTrue(Exists("UnrelatedCustomer")); break;
                case "create-before-marker-failure":
                    Error(() => Run("01-deploy.sql", true, inject: "before-marker"), 51992);
                    AssertOwned(0); Assert.IsFalse(Exists(Names[0])); Deploy(); AssertOwned(3); break;
                case "different-guid-rerun":
                    Deploy(); var old = Inventory(); Error(() => Run("01-deploy.sql", true, deployment: Guid.NewGuid()), 51000); Assert.AreEqual(old, Inventory()); break;
                case "wrong-column-type":
                    Sql("ALTER TABLE dbo.InvoiceWorkQuote ALTER COLUMN WorkQuoteId bigint NULL;");
                    var wrongColumn = Inventory(); Error(Deploy, 51000); Assert.AreEqual(wrongColumn, Inventory()); break;
                case "approval-gates":
                    Error(() => Run("01-deploy.sql", true, noWindow: true), 51000);
                    Error(() => Run("01-deploy.sql", true, noId: true), 51000); AssertOwned(0); break;
                case "offline-build-blocking": await OfflineBlocking(); break;
                case "business-regression": BusinessRegression(); break;
                default: Assert.Fail("Unknown rehearsal case."); break;
            }
            VerifyHashes(); _report["outcome"] = "PASSED";
        }
        catch (Exception ex)
        {
            if (!created) _cleanupUncertain = true;
            _report["outcome"] = "FAILED"; _report["error"] = ex.ToString(); throw;
        }
        finally
        {
            Exception? cleanupError = null;
            if (created)
            {
                try { await _database.DeleteAsync(); _report["cleanup"] = "Guarded deletion succeeded"; }
                catch (Exception ex) { _cleanupUncertain = true; cleanupError = ex; _report["cleanup"] = ex.ToString(); }
            }
            File.WriteAllText(Path.Combine(_artifact, "rehearsal.json"), JsonSerializer.Serialize(_report, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.WriteLine("Database: " + _database.DatabaseName + "; report: " + _artifact);
            if (cleanupError != null) throw new InvalidOperationException("Cleanup uncertain: stop, do not automatically rerun.", cleanupError);
        }
    }

    private void VerifyHashes()
    {
        var hashes = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(Path.Combine(_directory, "SHA256SUMS.txt")))
        {
            var parts = line.Split("  ", 2, StringSplitOptions.None); Assert.AreEqual(2, parts.Length);
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(_directory, parts[1])))).ToLowerInvariant();
            Assert.AreEqual(parts[0], actual, "Reviewed package checksum changed: " + parts[1]); hashes[parts[1]] = actual;
        }
        _report["packageHashes"] = hashes;
    }

    private void Deploy() => Run("01-deploy.sql", true);
    private void Rollback() => Run("02-rollback.sql", true);
    private void Run(string file, bool execute = false, string? inject = null, Guid? deployment = null,
        bool originalGuard = false, bool wrongExpected = false, bool noWindow = false, bool noId = false)
    {
        using var connection = _database.OpenOwnedConnection();
        RunOn(connection, file, execute, inject, deployment, originalGuard, wrongExpected, noWindow, noId);
    }
    private void RunOn(SqlConnection connection, string file, bool execute, string? inject = null, Guid? deployment = null,
        bool originalGuard = false, bool wrongExpected = false, bool noWindow = false, bool noId = false)
    {
        Assert.AreEqual(_database.DatabaseName, connection.Database); DisposableSqlDatabase.ValidateCatalog(connection.Database);
        var sql = File.ReadAllText(Path.Combine(_directory, file)).Replace("\r\n", "\n");
        const string oldGuard = "IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'Garagesaas' COLLATE Latin1_General_100_BIN2\n    THROW 51000, 'Wrong database. Connect explicitly to Garagesaas; this script never switches catalogs.', 1;";
        Assert.AreEqual(1, Count(sql, oldGuard));
        if (!originalGuard)
            sql = sql.Replace(oldGuard, @"IF DB_NAME() COLLATE Latin1_General_100_BIN2 = N'Garagesaas' COLLATE Latin1_General_100_BIN2
 OR DB_NAME() COLLATE Latin1_General_100_BIN2 <> @HarnessDatabase COLLATE Latin1_General_100_BIN2
 OR ISNULL((SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'GarageSaasTestOwner'),N'') COLLATE Latin1_General_100_BIN2 <> @HarnessOwner COLLATE Latin1_General_100_BIN2
    THROW 51000, 'Rehearsal target/ownership mismatch. Application databases are forbidden.', 1;");
        if (execute) sql = sql.Replace("DECLARE @Execute bit = 0;", "DECLARE @Execute bit = 1;");
        if (execute && !noWindow) sql = sql.Replace("DECLARE @MaintenanceWindowConfirmed bit = 0;", "DECLARE @MaintenanceWindowConfirmed bit = 1;");
        if (!noId) sql = sql.Replace("DECLARE @DeploymentId uniqueidentifier = NULL;", $"DECLARE @DeploymentId uniqueidentifier = '{deployment ?? _deployment:D}';");
        if (inject == "before-second")
        {
            const string position = "        IF NOT EXISTS (SELECT 1 FROM @Existing WHERE Ord=@Ord AND Equivalent=1)";
            Assert.AreEqual(1, Count(sql, position)); sql = sql.Replace(position, "        IF @Ord=2 THROW 51991, 'Injected before second index', 1;\n" + position);
        }
        if (inject == "before-marker")
        {
            const string position = "            SET @CreatedUtc=CONVERT";
            Assert.AreEqual(1, Count(sql, position)); sql = sql.Replace(position, "            THROW 51992, 'Injected after CREATE before ownership', 1;\n" + position);
        }
        using var command = connection.CreateCommand(); command.CommandTimeout = 30; command.CommandText = sql;
        command.Parameters.Add("@HarnessDatabase", SqlDbType.NVarChar, 128).Value = wrongExpected ? "GarageSaas_InvoiceAtomic_Test_" + Guid.NewGuid().ToString("N") : _database.DatabaseName;
        command.Parameters.Add("@HarnessOwner", SqlDbType.NVarChar, 128).Value = Rows(connection,
            "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'GarageSaasTestOwner'").Single()[0];
        var resultSets = new List<List<string[]>>(); var messages = new List<string>();
        SqlInfoMessageEventHandler handler = (_, e) => messages.Add(e.Message); connection.InfoMessage += handler;
        try
        {
            using var reader = command.ExecuteReader();
            do
            {
                var set = new List<string[]>();
                if (reader.FieldCount > 0) set.Add(Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray());
                while (reader.Read()) set.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "<NULL>" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)!).ToArray());
                resultSets.Add(set);
            } while (reader.NextResult());
            _events.Add(new { file, execute, inject, originalGuard, outcome = "completed", messages, resultSets });
        }
        catch (SqlException ex)
        {
            _events.Add(new { file, execute, inject, originalGuard, outcome = "SQL error", ex.Number, ex.Message, ex.LineNumber, messages, resultSets }); throw;
        }
        finally { connection.InfoMessage -= handler; }
    }
    private static int Count(string input, string value) => (input.Length - input.Replace(value, "").Length) / value.Length;
    private void Error(Action action, int number)
    {
        var ex = Assert.ThrowsException<SqlException>(action); Assert.AreEqual(number, ex.Number, ex.ToString());
        _events.Add(new { expectedSqlError = number, ex.Message });
    }
    private void Sql(string sql)
    {
        using var c = _database.OpenOwnedConnection(); using var command = c.CreateCommand(); command.CommandTimeout = 8; command.CommandText = sql; command.ExecuteNonQuery();
    }
    private static List<string[]> Rows(SqlConnection connection, string sql)
    {
        using var command = connection.CreateCommand(); command.CommandText = sql; command.CommandTimeout = 8;
        using var reader = command.ExecuteReader(); var rows = new List<string[]>();
        while (reader.Read()) rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "<NULL>" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)!).ToArray()); return rows;
    }
    private string Inventory()
    {
        using var c = _database.OpenOwnedConnection();
        return JsonSerializer.Serialize(Rows(c, @"SELECT t.name,i.name,i.index_id,i.type,i.is_unique,i.is_disabled,c.name,k.key_ordinal,k.is_descending_key,k.is_included_column,
            CONVERT(nvarchar(128),p.value),CONVERT(nvarchar(128),d.value),CONVERT(nvarchar(128),u.value)
            FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id JOIN sys.index_columns k ON k.object_id=i.object_id AND k.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
            LEFT JOIN sys.extended_properties p ON p.class=7 AND p.major_id=i.object_id AND p.minor_id=i.index_id AND p.name=N'GarageSaas.IndexPackage'
            LEFT JOIN sys.extended_properties d ON d.class=7 AND d.major_id=i.object_id AND d.minor_id=i.index_id AND d.name=N'GarageSaas.DeploymentId'
            LEFT JOIN sys.extended_properties u ON u.class=7 AND u.major_id=i.object_id AND u.minor_id=i.index_id AND u.name=N'GarageSaas.CreatedUtc'
            WHERE t.name IN ('VehicleInvoice','InvoiceWorkQuote') ORDER BY t.name,i.index_id,k.index_column_id"));
    }
    private bool Exists(string name)
    {
        using var c = _database.OpenOwnedConnection(); using var command = c.CreateCommand(); command.CommandTimeout = 8;
        command.CommandText = "SELECT COUNT(*) FROM sys.indexes WHERE name=@name AND object_id IN (OBJECT_ID('dbo.VehicleInvoice'),OBJECT_ID('dbo.InvoiceWorkQuote'))";
        command.Parameters.AddWithValue("@name", name); return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }
    private int OwnerCount()
    {
        using var c = _database.OpenOwnedConnection(); return int.Parse(Rows(c, $"SELECT COUNT(*) FROM sys.extended_properties WHERE class=7 AND name=N'GarageSaas.IndexPackage' AND CONVERT(nvarchar(128),value)=N'{Package}'").Single()[0]);
    }
    private void AssertOwned(int count)
    {
        Assert.AreEqual(count, OwnerCount());
        if (count == 3)
        {
            using var c = _database.OpenOwnedConnection();
            Assert.AreEqual("3", Rows(c, $"SELECT COUNT(*) FROM sys.extended_properties WHERE class=7 AND name=N'GarageSaas.DeploymentId' AND TRY_CONVERT(uniqueidentifier,CONVERT(nvarchar(128),value))='{_deployment:D}'").Single()[0]);
        }
    }
    private void VerifyDefinitions()
    {
        using var c = _database.OpenOwnedConnection();
        Assert.AreEqual("3", Rows(c, "SELECT COUNT(*) FROM sys.indexes WHERE object_id IN (OBJECT_ID('dbo.VehicleInvoice'),OBJECT_ID('dbo.InvoiceWorkQuote')) AND index_id>0 AND is_primary_key=0").Single()[0]);
        var rows = Rows(c, @"SELECT t.name,i.name,c.name,k.key_ordinal,k.is_included_column,k.is_descending_key,i.is_unique,i.has_filter,i.is_disabled
            FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id JOIN sys.index_columns k ON k.object_id=i.object_id AND k.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id WHERE i.name LIKE 'IX[_]VehicleInvoice[_]WorkQuoteId' OR i.name LIKE 'IX[_]InvoiceWorkQuote[_]%'
            ORDER BY i.name,k.key_ordinal,c.name");
        Assert.AreEqual(8, rows.Count);
        Assert.IsTrue(rows.All(x => x[5] == "False" && x[6] == "False" && x[7] == "False" && x[8] == "False"));
        var expected = new[]
        {
            Names[0]+"|WorkQuoteId|1|False", Names[0]+"|GarageBusinessId|0|True",
            Names[1]+"|WorkQuoteId|1|False", Names[1]+"|InvoiceId|2|False", Names[1]+"|GarageBusinessCustomerId|0|True",
            Names[2]+"|InvoiceId|1|False", Names[2]+"|WorkQuoteId|2|False", Names[2]+"|GarageBusinessCustomerId|0|True"
        };
        CollectionAssert.AreEquivalent(expected, rows.Select(x => string.Join("|", x.Skip(1).Take(4))).ToArray());
        _events.Add(new { verifiedDefinitions = rows });
    }

    private async Task OfflineBlocking()
    {
        Sql("WITH n AS(SELECT 1 x UNION ALL SELECT x+1 FROM n WHERE x<100) INSERT dbo.VehicleInvoice(CreatedDate,CreatedBy,InvoiceStatus) SELECT '20260101','synthetic','Pending' FROM n OPTION(MAXRECURSION 0);");
        using var blocker = _database.OpenOwnedConnection(); var blockerId = int.Parse(Rows(blocker, "SELECT @@SPID").Single()[0]);
        using var transaction = blocker.BeginTransaction();
        using (var hold = blocker.CreateCommand())
        { hold.Transaction = transaction; hold.CommandTimeout = 8; hold.CommandText = "SELECT COUNT(*) FROM dbo.VehicleInvoice WITH(UPDLOCK,HOLDLOCK)"; hold.ExecuteScalar(); }
        using var reached = new ManualResetEventSlim(); var workerId = 0;
        var worker = _database.Track(Task.Run(() =>
        {
            using var c = _database.OpenOwnedConnection(); workerId = int.Parse(Rows(c, "SELECT @@SPID").Single()[0]); reached.Set(); RunOn(c, "01-deploy.sql", true);
        }));
        var waits = new List<string[]>();
        try
        {
            Assert.IsTrue(reached.Wait(TimeSpan.FromSeconds(5)));
            using var monitor = _database.OpenOwnedConnection();
            for (var i=0;i<20 && waits.Count==0 && !worker.IsCompleted;i++)
            {
                await Task.Delay(50);
                waits = Rows(monitor, $@"SELECT r.session_id,r.blocking_session_id,r.wait_type,r.wait_time,r.wait_resource,
                    SUBSTRING(t.text,r.statement_start_offset/2+1,(CASE WHEN r.statement_end_offset=-1 THEN DATALENGTH(t.text) ELSE r.statement_end_offset END-r.statement_start_offset)/2+1)
                    FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
                    WHERE r.database_id=DB_ID() AND r.session_id={workerId} AND r.blocking_session_id={blockerId}");
            }
            _report["offlineBuildWaits"] = waits;
            Assert.IsTrue(waits.Count>0, "No blocking evidence captured; do not claim an offline blocking result.");
        }
        finally { transaction.Rollback(); await _database.DrainAsync(); }
        await worker.WaitAsync(TimeSpan.FromSeconds(10)); AssertOwned(3);
    }

    private void BusinessRegression()
    {
        using var db = new SignupContext(_database.Options()); var graph = SqlInvoiceGraph.Seed(db);
        var quote = new WorkQuoteService(db).AddOrUpdateWorkQuote(new CombinedWorkQuoteWorkitem { CustomerId=graph.CustomerId,VehicleId=graph.VehicleId,WorkItemId=graph.WorkItemId,WorkQuoteDate=new DateTime(2026,1,1) },graph.GarageId,"rehearsal");
        Assert.IsTrue(quote.Success,quote.ErrorMessage); Deploy();
        var service = new VehicleInvoiceService(db); var conversion = service.CreateFromWorkQuote(quote.Data.WorkQuoteId,graph.GarageId,"rehearsal");
        Assert.IsTrue(conversion.Success,conversion.ErrorMessage);
        Assert.IsFalse(service.CreateFromWorkQuote(quote.Data.WorkQuoteId,graph.GarageId,"rehearsal").Success);
        Assert.IsFalse(service.DeleteVehicleInvoice(conversion.Data.Id,graph.GarageId).Success);
        Assert.IsTrue(service.MarkInvoiceAsPaid(conversion.Data.Id,graph.GarageId,"rehearsal").Success);
        Assert.IsFalse(new WorkQuoteService(db).DeleteWorkQuote(quote.Data.WorkQuoteId,graph.GarageId).Success);
        Assert.AreEqual($"INV-{conversion.Data.InvoiceDate!.Value:yyyy}-{conversion.Data.Id:D6}",conversion.Data.InvoiceNumber);
        foreach (var paid in new[] { false, true })
        {
            int id; string number;
            using (var create = new SignupContext(_database.Options()))
            {
                var result = new VehicleInvoiceService(create).AddOrUpdateVehicleInvoice(new VehicleInvoice
                { CustomerId=graph.CustomerId,GarageBusinessCustomerId=graph.CustomerId,VehicleId=graph.VehicleId,InvoiceDate=new DateTime(2026,1,1),InvoiceStatus="Pending" },graph.GarageId,"rehearsal");
                Assert.IsTrue(result.Success,result.ErrorMessage); id=result.Data.Id; number=result.Data.InvoiceNumber;
            }
            using (var edit = new SignupContext(_database.Options()))
            {
                var serviceEdit = new VehicleInvoiceService(edit);
                var result = serviceEdit.AddOrUpdateVehicleInvoice(new VehicleInvoice
                { Id=id,InvoiceNumber=number,CustomerId=graph.CustomerId,GarageBusinessCustomerId=graph.CustomerId,VehicleId=graph.VehicleId,InvoiceDate=new DateTime(2026,1,1),InvoiceStatus="Pending",Paid=false,Comment="Edited" },graph.GarageId,"rehearsal");
                Assert.IsTrue(result.Success,result.ErrorMessage);
                if (paid) Assert.IsTrue(serviceEdit.MarkInvoiceAsPaid(id,graph.GarageId,"rehearsal").Success);
            }
            using var delete = new SignupContext(_database.Options());
            Assert.IsTrue(new VehicleInvoiceService(delete).DeleteVehicleInvoice(id,graph.GarageId).Success);
        }
        _events.Add(new { businessChecks = "Same-garage WorkItem -> Quote -> Invoice; duplicate conversion/linked invoice deletion/linked quote deletion rejected; payment succeeds; number format retained; quote-free manual CREATE/ordinary UPDATE and unlinked paid/unpaid DELETE succeed." });
    }
}

using System.Text.Json;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GarageSaas.UnitTests.Services;

// Explicitly approved experiment only. The application catalog is referenced
// exclusively through fixed sys-catalog SELECTs, never business-table queries.
[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlIndexExperiment")]
public partial class AssociationIndexExperimentSqlTests
{
    private const string Flag = "GARAGESAAS_RUN_ASSOCIATION_INDEX_EXPERIMENT_SQL_TESTS";
    private readonly Dictionary<string, object> _report = new();
    private string _artifactDirectory = null!;
    public TestContext TestContext { get; set; } = null!;
    private const string Tables = "('VehicleInvoice','WorkQuote','InvoiceWorkQuote','WorkQuoteWorkItem','WorkItem','GarageBusiness','GarageBusinessCustomer','CustomerVehicle','CustomerOwnedVehicles')";

    [TestMethod]
    public async Task AlignedAssociationIndexesBeforeAfterExperiment()
    {
        var database = DisposableSqlDatabase.Start(Flag, "InvoiceAtomic");
        var created = false;
        _report["database"] = database.DatabaseName;
        // MSTest deletes successful deployment directories. Keep diagnostic
        // evidence outside that transient tree, under the project's TestResults.
        _artifactDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestResults", "AssociationIndexExperiments", database.DatabaseName));
        Directory.CreateDirectory(_artifactDirectory);
        try
        {
            database.Create(); created = true;
            using (var connection = database.OpenOwnedConnection())
            {
                _report["identity"] = Rows(connection, "SELECT ORIGINAL_LOGIN(),SUSER_SNAME(),SERVERPROPERTY('ServerName'),CONNECTIONPROPERTY('net_transport'),DB_NAME()");
                var deployed = Schema(connection, true); var before = Schema(connection, false);
                _report["deployedSchema"] = deployed; _report["efSchema"] = before;
                Align(connection);
                var after = Schema(connection, false); _report["alignedSchema"] = after;
                var differences = after.Except(deployed).Select(x => "TEST ONLY: " + x)
                    .Concat(deployed.Except(after).Select(x => "DEPLOYED ONLY: " + x)).ToArray();
                _report["schemaDifferencesAfterAlignment"] = differences;
                // Only the three explicitly approved width differences may remain.
                // GarageBusiness is read as EXISTS(Id), never projected/updated
                // by this measured workload; seed values fit both definitions.
                var allowed = new[] { "BusinessRegistrationNumber", "VatNumber", "LogoImage" };
                var material = differences.Where(x => !allowed.Any(column =>
                    x.StartsWith("TEST ONLY: COLUMN|dbo|GarageBusiness|" + column + "|", StringComparison.Ordinal) ||
                    x.StartsWith("DEPLOYED ONLY: COLUMN|dbo|GarageBusiness|" + column + "|", StringComparison.Ordinal))).ToArray();
                Assert.AreEqual(6, differences.Length, "Unexpected residual metadata; inspect report before measuring.");
                Assert.AreEqual(0, material.Length, "STOP: material schema differences. See experiment JSON.");
                foreach (var column in allowed)
                {
                    var prefix = "COLUMN|dbo|GarageBusiness|" + column + "|";
                    var test = after.Single(x => x.StartsWith(prefix, StringComparison.Ordinal)).Split('|');
                    var app = deployed.Single(x => x.StartsWith(prefix, StringComparison.Ordinal)).Split('|');
                    Assert.AreEqual("510", test[5]);
                    Assert.AreEqual(column == "LogoImage" ? "1000" : "100", app[5]);
                    test[5] = app[5]; CollectionAssert.AreEqual(app, test, "Only approved lengths may differ.");
                }
                _report["residualClassification"] = "NON-MATERIAL for this workload only: three GarageBusiness nvarchar widths. The service reads EXISTS(Id); no measured parent-table writes, width-dependent predicates or projections. Seed leaves all three NULL. Complete schema parity is not claimed.";
            }
            await RunMeasurements(database);
        }
        finally
        {
            if (created)
            {
                try { await database.DeleteAsync(); _report["cleanup"] = "Guarded deletion succeeded"; }
                catch (Exception ex) { _report["cleanup"] = ex.ToString(); throw; }
            }
            var path = Path.Combine(_artifactDirectory, "association-index-experiment.json");
            File.WriteAllText(path, JsonSerializer.Serialize(_report, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.WriteLine("Experiment: " + database.DatabaseName + "; report: " + path);
        }
    }

    internal void Align(SqlConnection connection)
    {
        var foreignKeys = Rows(connection, @"SELECT fk.name,c.name,r.name FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns f ON f.constraint_object_id=fk.object_id
            JOIN sys.columns c ON c.object_id=f.parent_object_id AND c.column_id=f.parent_column_id
            JOIN sys.tables r ON r.object_id=f.referenced_object_id
            WHERE fk.parent_object_id=OBJECT_ID(N'dbo.VehicleInvoice') ORDER BY c.name");
        Assert.AreEqual(2, foreignKeys.Count, "Unexpected VehicleInvoice FK shape; no DDL permitted.");
        Assert.IsTrue(foreignKeys.Any(x => x[1] == "GarageBusinessCustomerId" && x[2] == "GarageBusinessCustomer"));
        Assert.IsTrue(foreignKeys.Any(x => x[1] == "VehicleId" && x[2] == "CustomerVehicle"));
        var indexes = Rows(connection, @"SELECT i.name,c.name,CONVERT(varchar(1),i.is_unique),CONVERT(varchar(1),ic.is_included_column)
            FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE i.object_id=OBJECT_ID(N'dbo.VehicleInvoice') AND i.is_primary_key=0 AND i.index_id>0 ORDER BY c.name");
        Assert.AreEqual(2, indexes.Count, "Unexpected VehicleInvoice indexes; no DDL permitted.");
        CollectionAssert.AreEquivalent(new[] { "GarageBusinessCustomerId", "VehicleId" }, indexes.Select(x => x[1]).ToArray());
        Assert.IsTrue(indexes.All(x => x[2] == "0" && x[3] == "0"));
        _report["removedForeignKeys"] = foreignKeys; _report["removedIndexes"] = indexes;
        foreach (var fk in foreignKeys) Execute(connection, "ALTER TABLE dbo.VehicleInvoice DROP CONSTRAINT " + Quote(fk[0]));
        foreach (var index in indexes) Execute(connection, "DROP INDEX " + Quote(index[0]) + " ON dbo.VehicleInvoice");
        var parentKeys = Rows(connection, @"SELECT fk.name,c.name,r.name FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns f ON f.constraint_object_id=fk.object_id
            JOIN sys.columns c ON c.object_id=f.parent_object_id AND c.column_id=f.parent_column_id
            JOIN sys.tables r ON r.object_id=f.referenced_object_id
            WHERE fk.parent_object_id=OBJECT_ID(N'dbo.CustomerVehicle') AND r.name IN ('VehicleMake','VehicleModel') ORDER BY c.name");
        Assert.AreEqual(2, parentKeys.Count);
        Assert.IsTrue(parentKeys.Any(x => x[1] == "VehicleMakeId" && x[2] == "VehicleMake"));
        Assert.IsTrue(parentKeys.Any(x => x[1] == "VehicleModelId" && x[2] == "VehicleModel"));
        var parentIndexes = Rows(connection, @"SELECT t.name,i.name,c.name,i.is_unique,ic.is_included_column
            FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id
            JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE t.name IN ('CustomerVehicle','CustomerOwnedVehicles','GarageBusinessCustomer') AND i.is_primary_key=0 ORDER BY t.name,c.name");
        CollectionAssert.AreEquivalent(new[] { "CustomerVehicle.GarageBusinessId", "CustomerVehicle.VehicleMakeId", "CustomerVehicle.VehicleModelId", "CustomerOwnedVehicles.GarageBusinessId", "GarageBusinessCustomer.GarageBusinessId" }, parentIndexes.Select(x => x[0] + "." + x[2]).ToArray());
        Assert.IsTrue(parentIndexes.All(x => x[3] == "False" && x[4] == "False"));
        _report["removedParentForeignKeys"] = parentKeys; _report["removedParentIndexes"] = parentIndexes;
        foreach (var fk in parentKeys) Execute(connection, "ALTER TABLE dbo.CustomerVehicle DROP CONSTRAINT " + Quote(fk[0]));
        foreach (var index in parentIndexes) Execute(connection, "DROP INDEX " + Quote(index[1]) + " ON dbo." + Quote(index[0]));
    }

    private static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";
    private static void Execute(SqlConnection connection, string sql)
    {
        DisposableSqlDatabase.ValidateCatalog(connection.Database);
        using var command = connection.CreateCommand(); command.CommandTimeout = 30; command.CommandText = sql; command.ExecuteNonQuery();
    }

    private static List<string[]> Rows(SqlConnection connection, string sql)
    {
        using var command = connection.CreateCommand(); command.CommandTimeout = 8; command.CommandText = sql;
        using var reader = command.ExecuteReader(); var rows = new List<string[]>();
        while (reader.Read()) rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "<NULL>" : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture)!).ToArray());
        return rows;
    }

    internal static string[] Schema(SqlConnection connection, bool deployed)
    {
        // The only deployed references in this fixture are these fixed catalog
        // views. No dynamic catalog supplied by an environment variable/user.
        var s = deployed ? "Garagesaas.sys." : "sys.";
        var queries = new[]
        {
            $@"SELECT 'COLUMN',sc.name,t.name,c.name,ty.name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,c.is_computed,c.collation_name,dc.definition,cc.definition
                FROM {s}tables t JOIN {s}schemas sc ON sc.schema_id=t.schema_id JOIN {s}columns c ON c.object_id=t.object_id
                JOIN {s}types ty ON ty.user_type_id=c.user_type_id LEFT JOIN {s}default_constraints dc ON dc.object_id=c.default_object_id
                LEFT JOIN {s}computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id WHERE t.name IN {Tables}",
            $@"SELECT 'INDEX',sc.name,t.name,i.type_desc,i.is_unique,i.is_primary_key,i.is_unique_constraint,i.has_filter,i.filter_definition,c.name,ic.key_ordinal,ic.is_descending_key,ic.is_included_column,i.is_disabled
                FROM {s}tables t JOIN {s}schemas sc ON sc.schema_id=t.schema_id JOIN {s}indexes i ON i.object_id=t.object_id
                JOIN {s}index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
                JOIN {s}columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE t.name IN {Tables}",
            $@"SELECT 'FK',sc.name,t.name,c.name,rs.name,r.name,rc.name,f.constraint_column_id,fk.delete_referential_action_desc,fk.update_referential_action_desc,fk.is_disabled,fk.is_not_trusted
                FROM {s}foreign_keys fk JOIN {s}foreign_key_columns f ON f.constraint_object_id=fk.object_id
                JOIN {s}tables t ON t.object_id=f.parent_object_id JOIN {s}schemas sc ON sc.schema_id=t.schema_id
                JOIN {s}columns c ON c.object_id=t.object_id AND c.column_id=f.parent_column_id
                JOIN {s}tables r ON r.object_id=f.referenced_object_id JOIN {s}schemas rs ON rs.schema_id=r.schema_id
                JOIN {s}columns rc ON rc.object_id=r.object_id AND rc.column_id=f.referenced_column_id WHERE t.name IN {Tables}",
            $@"SELECT 'CHECK',t.name,c.definition,c.is_disabled,c.is_not_trusted FROM {s}check_constraints c JOIN {s}tables t ON t.object_id=c.parent_object_id WHERE t.name IN {Tables}",
            $@"SELECT 'TRIGGER',t.name,tr.name,tr.is_disabled,m.definition FROM {s}triggers tr JOIN {s}tables t ON t.object_id=tr.parent_id LEFT JOIN {s}sql_modules m ON m.object_id=tr.object_id WHERE t.name IN {Tables}",
            "SELECT 'DATABASE',compatibility_level,collation_name,is_read_committed_snapshot_on,snapshot_isolation_state_desc,is_auto_create_stats_on,is_auto_update_stats_on,is_auto_update_stats_async_on FROM sys.databases WHERE name=" + (deployed ? "N'Garagesaas'" : "DB_NAME()"),
            $"SELECT 'SCOPED',name,value,value_for_secondary FROM {s}database_scoped_configurations WHERE name IN ('LEGACY_CARDINALITY_ESTIMATION','PARAMETER_SNIFFING','QUERY_OPTIMIZER_HOTFIXES','MAXDOP','PARAMETER_SENSITIVE_PLAN_OPTIMIZATION')"
        };
        return queries.SelectMany(sql => Rows(connection, sql)).Select(row => string.Join("|", row)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
}

using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using GarageSaas.Services;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

public partial class AssociationIndexExperimentSqlTests
{
    private static readonly string[] CandidateDdl =
    {
        "CREATE NONCLUSTERED INDEX IX_VehicleInvoice_WorkQuoteId ON dbo.VehicleInvoice (WorkQuoteId) INCLUDE (GarageBusinessId)",
        "CREATE NONCLUSTERED INDEX IX_InvoiceWorkQuote_WorkQuoteId_InvoiceId ON dbo.InvoiceWorkQuote (WorkQuoteId, InvoiceId) INCLUDE (GarageBusinessCustomerId)",
        "CREATE NONCLUSTERED INDEX IX_InvoiceWorkQuote_InvoiceId_WorkQuoteId ON dbo.InvoiceWorkQuote (InvoiceId, WorkQuoteId) INCLUDE (GarageBusinessCustomerId)"
    };

    private async Task RunMeasurements(DisposableSqlDatabase database)
    {
        var measurements = new List<object>(); _report["measurements"] = measurements;
        var fingerprints = new Dictionary<int, string>();
        foreach (var stage in new[] { "baseline", "indexed" })
        {
            if (stage == "indexed")
            {
                using var ddl = database.OpenOwnedConnection();
                foreach (var sql in CandidateDdl) Execute(ddl, sql);
                _report["candidateDdl"] = CandidateDdl;
            }
            foreach (var garages in new[] { 30, 300, 3000 })
            {
                using (var seed = database.OpenOwnedConnection())
                {
                    Seed(seed, garages);
                    var fingerprint = string.Join(";", Rows(seed, @"SELECT 'VehicleInvoice',COUNT_BIG(*),CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.VehicleInvoice
                        UNION ALL SELECT 'WorkQuote',COUNT_BIG(*),CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.WorkQuote
                        UNION ALL SELECT 'InvoiceWorkQuote',COUNT_BIG(*),CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.InvoiceWorkQuote
                        UNION ALL SELECT 'WorkQuoteWorkItem',COUNT_BIG(*),CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.WorkQuoteWorkItem").Select(x => string.Join("|", x)));
                    if (stage == "baseline") fingerprints[garages] = fingerprint;
                    else Assert.AreEqual(fingerprints[garages], fingerprint, "Before/after synthetic rows differ.");
                    measurements.Add(new { stage, garages, fingerprint, distribution = Rows(seed, "SELECT COUNT(*) GarageCount,MIN(n) MinInvoices,MAX(n) MaxInvoices,SUM(n) TotalInvoices FROM (SELECT GarageBusinessId,COUNT(*) n FROM VehicleInvoice GROUP BY GarageBusinessId) x") });
                }
                var trace = new Trace();
                var durations = new List<double>();
                for (var trial = 0; trial < 5; trial++)
                {
                    using var context = Context(database, trace);
                    var clock = Stopwatch.StartNew();
                    var result = new VehicleInvoiceService(context).AddOrUpdateVehicleInvoice(Input(garages, garages * 1000 + 4, garages * 1000 + 2), garages, "experiment");
                    durations.Add(clock.Elapsed.TotalMilliseconds);
                    Assert.IsTrue(result.Success, result.ErrorMessage);
                    using var reset = database.OpenOwnedConnection(); ResetProbe(reset, garages);
                }
                // Capture the existing deletion absence-check SQL too, without
                // removing a baseline row. Create and delete one isolated probe.
                using (var context = Context(database, trace))
                {
                    var service = new VehicleInvoiceService(context);
                    var created = service.AddOrUpdateVehicleInvoice(Input(garages, 0, null), garages, "experiment");
                    Assert.IsTrue(created.Success, created.ErrorMessage);
                    Assert.IsTrue(service.DeleteVehicleInvoice(created.Data.Id, garages).Success);
                }
                var commands = trace.Commands.ToArray();
                Assert.IsFalse(commands.Any(c => c.Sql.Contains("BusinessRegistrationNumber") || c.Sql.Contains("LogoImage") || c.Sql.Contains("VatNumber")), "Width exception invalid: measured EF SQL references differing columns.");
                measurements.Add(new { stage, garages, serviceTransactionMilliseconds = durations, efCommands = commands });
                var selects = commands.Where(c => c.Sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                    .Where(c => c.Sql.Contains("[VehicleInvoice]") || c.Sql.Contains("[InvoiceWorkQuote]"))
                    .GroupBy(c => c.Sql).Select(g => g.First()).ToArray();
                Assert.IsTrue(selects.Any(c => c.Sql.Contains("[WorkQuoteId]")), "No association SQL captured.");
                foreach (var select in selects)
                    measurements.Add(Replay(database, stage, garages, select));
                measurements.Add(WriteCosts(database, stage, garages));
                if (garages == 3000)
                {
                    foreach (var schedule in new[] { "invoice-write", "invoice-and-join-write", "same-quote" })
                        measurements.Add(await Schedule(database, stage, garages, schedule));
                    measurements.Add(Correctness(database, stage, garages));
                }
                using var storage = database.OpenOwnedConnection();
                measurements.Add(new { stage, garages, indexStorage = Rows(storage, @"SELECT OBJECT_NAME(p.object_id),i.name,SUM(p.row_count),SUM(p.used_page_count)*8 UsedKB,SUM(p.reserved_page_count)*8 ReservedKB
                    FROM sys.dm_db_partition_stats p JOIN sys.indexes i ON i.object_id=p.object_id AND i.index_id=p.index_id
                    WHERE p.object_id IN (OBJECT_ID('VehicleInvoice'),OBJECT_ID('InvoiceWorkQuote'))
                    GROUP BY p.object_id,i.name ORDER BY OBJECT_NAME(p.object_id),i.name") });
            }
        }
    }

    private static SignupContext Context(DisposableSqlDatabase database, Trace trace)
    {
        // Keep the verified owned connection open for the entire EF context;
        // it is explicitly disposed with the context through this subclass.
        var connection = database.OpenOwnedConnection();
        trace.Spid = int.Parse(Rows(connection, "SELECT @@SPID").Single()[0], CultureInfo.InvariantCulture);
        var builder = new DbContextOptionsBuilder<SignupContext>();
        builder.UseSqlServer(connection, x => x.CommandTimeout(8)); builder.AddInterceptors(trace);
        return new OwnedContext(builder.Options, connection);
    }
    private sealed class OwnedContext : SignupContext
    {
        private readonly SqlConnection _connection;
        internal OwnedContext(DbContextOptions<SignupContext> options, SqlConnection connection) : base(options) => _connection = connection;
        public override void Dispose() { base.Dispose(); _connection.Dispose(); }
    }
    private static VehicleInvoice Input(int garage, int invoice, int? quote) => new()
    {
        Id = invoice, CustomerId = garage + 10000, GarageBusinessCustomerId = garage + 10000,
        VehicleId = garage + 20000, WorkQuoteId = quote, InvoiceDate = new DateTime(2026, 1, 1),
        InvoiceNumber = invoice == 0 ? null : $"INV-2026-{invoice:D6}", InvoiceStatus = "Pending", Paid = false, Comment = "seed"
    };
    private static void ResetProbe(SqlConnection connection, int garage) => Execute(connection,
        $"UPDATE dbo.VehicleInvoice SET WorkQuoteId=NULL,UpdatedDate=NULL,UpdatedBy=NULL WHERE Id={garage * 1000 + 4}");

    private static void Seed(SqlConnection connection, int garages)
    {
        Assert.IsTrue(new[] { 30, 300, 3000 }.Contains(garages));
        // Fixed synthetic IDs and timestamps, independent of identity counters.
        // This is fixture setup, not part of any measured write set.
        Execute(connection, @"DELETE FROM InvoiceWorkQuote; DELETE FROM WorkQuoteWorkItem; DELETE FROM VehicleInvoice;
            DELETE FROM WorkQuote; DELETE FROM WorkItem; DELETE FROM CustomerOwnedVehicles;
            DELETE FROM GarageBusinessCustomer; DELETE FROM CustomerVehicle; DELETE FROM GarageBusiness;
            CREATE TABLE #g (g int PRIMARY KEY,n int NOT NULL);
            WITH n AS (SELECT 1 g UNION ALL SELECT g+1 FROM n WHERE g<" + garages + @")
            INSERT #g SELECT g,CASE WHEN g%50=0 THEN 200 WHEN g%10=0 THEN 20 ELSE 4 END FROM n OPTION(MAXRECURSION 0);
            SET IDENTITY_INSERT GarageBusiness ON;
            INSERT GarageBusiness(Id,GarageBusinessName,Active,Blocked,CreatedDate,CreatedBy) SELECT g,CONCAT('Synthetic ',g),1,0,'20260101','seed' FROM #g;
            SET IDENTITY_INSERT GarageBusiness OFF;
            SET IDENTITY_INSERT GarageBusinessCustomer ON;
            INSERT GarageBusinessCustomer(Id,GarageBusinessId,GarageCustomerForename,GarageCustomerSurname,CreatedDate,CreatedBy,Active,Blocked)
            SELECT g+10000,g,'Synthetic','Customer','20260101','seed',1,0 FROM #g;
            SET IDENTITY_INSERT GarageBusinessCustomer OFF;
            SET IDENTITY_INSERT CustomerVehicle ON;
            INSERT CustomerVehicle(Id,GarageBusinessId,VehicleRegistration,Active,Blocked,GarageOwned,CreatedDate,CreatedBy,VehicleMileageDate)
            SELECT g+20000,g,CONCAT('TEST',g),1,0,0,'20260101','seed','20260101' FROM #g;
            SET IDENTITY_INSERT CustomerVehicle OFF;
            CREATE TABLE #q (id int PRIMARY KEY,g int NOT NULL,k int NOT NULL);
            WITH n AS(SELECT 1 k UNION ALL SELECT k+1 FROM n WHERE k<200)
            INSERT #q SELECT g*1000+k,g,k FROM #g CROSS JOIN n WHERE k<=#g.n OPTION(MAXRECURSION 0);
            SET IDENTITY_INSERT WorkItem ON;
            INSERT WorkItem(Id,GarageBusinessCustomerId,CustomerId,VehicleId,RepairInstructions,CreatedDate,CreatedBy)
            SELECT id,g,g+10000,g+20000,CONCAT('Synthetic work ',id),'20260101','seed' FROM #q;
            SET IDENTITY_INSERT WorkItem OFF;
            SET IDENTITY_INSERT WorkQuote ON;
            INSERT WorkQuote(Id,GarageBusinessCustomerId,CustomerId,VehicleId,Comment,CreatedDate,CreatedBy)
            SELECT id,g,g+10000,g+20000,'seed','20260101','seed' FROM #q;
            SET IDENTITY_INSERT WorkQuote OFF;
            SET IDENTITY_INSERT WorkQuoteWorkItem ON;
            INSERT WorkQuoteWorkItem(Id,GarageBusinessCustomerId,WorkQuoteId,WorkItemId,CreatedDate,CreatedBy)
            SELECT id,g,id,id,'20260101','seed' FROM #q;
            SET IDENTITY_INSERT WorkQuoteWorkItem OFF;
            SET IDENTITY_INSERT VehicleInvoice ON;
            INSERT VehicleInvoice(Id,GarageBusinessId,GarageBusinessCustomerId,CustomerId,VehicleId,WorkQuoteId,InvoiceNumber,InvoiceDate,Paid,InvoiceStatus,Comment,CreatedDate,CreatedBy)
            SELECT id,g,g+10000,g+10000,g+20000,CASE WHEN k%2=1 THEN id END,CONCAT('INV-2026-',FORMAT(id,'D6')),'20260101',0,'Pending','seed','20260101','seed' FROM #q;
            SET IDENTITY_INSERT VehicleInvoice OFF;
            SET IDENTITY_INSERT InvoiceWorkQuote ON;
            INSERT InvoiceWorkQuote(Id,GarageBusinessCustomerId,InvoiceId,WorkQuoteId,CreatedDate,CreatedBy)
            SELECT id,g,id,id,'20260101','seed' FROM #q WHERE k%2=1;
            SET IDENTITY_INSERT InvoiceWorkQuote OFF;
            DROP TABLE #q; DROP TABLE #g;");
    }

    private sealed record Parameter(string Name, SqlDbType Type, int Size, byte Precision, byte Scale, object? Value);
    private sealed record Command(string Sql, Parameter[] Parameters)
    {
        internal static Command Capture(DbCommand c) => new(c.CommandText, c.Parameters.Cast<SqlParameter>()
            .Select(p => new Parameter(p.ParameterName, p.SqlDbType, p.Size, p.Precision, p.Scale, p.Value == DBNull.Value ? null : p.Value)).ToArray());
        internal void Bind(SqlCommand c)
        {
            foreach (var p in Parameters) c.Parameters.Add(new SqlParameter(p.Name, p.Type, p.Size) { Precision = p.Precision, Scale = p.Scale, Value = p.Value ?? DBNull.Value });
        }
    }
    private sealed class Trace : DbCommandInterceptor
    {
        internal ConcurrentQueue<Command> Commands { get; } = new();
        internal int Spid;
        private void Record(DbCommand command)
        {
            DisposableSqlDatabase.ValidateCatalog(command.Connection!.Database);
            Commands.Enqueue(Command.Capture(command));
        }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) { Record(command); return result; }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) { Record(command); return result; }
        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result) { Record(command); return result; }
    }

    private object Replay(DisposableSqlDatabase database, string stage, int garages, Command snapshot)
    {
        using var connection = database.OpenOwnedConnection();
        var messages = new List<string>(); connection.InfoMessage += (_, e) => messages.Add(e.Message);
        Execute(connection, "SET STATISTICS IO ON; SET STATISTICS XML ON;");
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandTimeout = 8;
        command.CommandText = snapshot.Sql; snapshot.Bind(command);
        var clock = Stopwatch.StartNew(); var plans = new List<string>(); long rows = 0;
        using (var reader = command.ExecuteReader())
        {
            do
            {
                var plan = reader.FieldCount == 1 && reader.GetName(0).StartsWith("Microsoft SQL Server", StringComparison.Ordinal);
                while (reader.Read()) { if (plan) plans.Add(reader.GetString(0)); else rows++; }
            } while (reader.NextResult());
        }
        var elapsed = clock.Elapsed.TotalMilliseconds; transaction.Rollback();
        Assert.IsTrue(plans.Count > 0, "STOP: actual execution plan capture unavailable.");
        Assert.IsTrue(messages.Any(x => x.Contains("logical reads", StringComparison.OrdinalIgnoreCase)), "STOP: logical read diagnostics unavailable.");
        var ns = (XNamespace)"http://schemas.microsoft.com/sqlserver/2004/07/showplan";
        var operators = plans.SelectMany(p => XDocument.Parse(p).Descendants(ns + "RelOp").Select(x => new
        {
            physical = (string?)x.Attribute("PhysicalOp"), logical = (string?)x.Attribute("LogicalOp"),
            objects = x.Descendants(ns + "Object").Select(o => new { table = (string?)o.Attribute("Table"), index = (string?)o.Attribute("Index") }).Distinct().ToArray()
        })).ToArray();
        var number = Interlocked.Increment(ref _planNumber);
        var files = new List<string>();
        foreach (var plan in plans)
        {
            var path = Path.Combine(_artifactDirectory, $"{stage}-{garages}-{number}-{files.Count}.sqlplan");
            File.WriteAllText(path, plan); files.Add(path);
        }
        return new { stage, garages, kind = "actual-plan-replay", snapshot, rows, elapsedMilliseconds = elapsed,
            logicalReads = messages.Sum(m => Regex.Matches(m, @"(?<!lob )logical reads (\d+)", RegexOptions.IgnoreCase).Sum(x => long.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture))), messages, operators, files };
    }
    private int _planNumber;

    private async Task<object> Schedule(DisposableSqlDatabase database, string stage, int garages, string schedule)
    {
        using var writer = database.OpenOwnedConnection();
        var writerSpid = int.Parse(Rows(writer, "SELECT @@SPID").Single()[0], CultureInfo.InvariantCulture);
        using var transaction = writer.BeginTransaction();
        var clock = Stopwatch.StartNew(); var invoice = schedule == "same-quote" ? garages * 1000 + 2 : 1002;
        var quote = schedule == "same-quote" ? garages * 1000 + 2 : 1002;
        using (var command = writer.CreateCommand())
        {
            command.Transaction = transaction; command.CommandTimeout = 8;
            if (schedule != "invoice-write")
            {
                command.CommandText = "SELECT Id FROM WorkQuote WITH (UPDLOCK,HOLDLOCK) WHERE Id=@quote AND GarageBusinessCustomerId=@garage";
                command.Parameters.Add("@quote", SqlDbType.Int).Value = quote;
                command.Parameters.Add("@garage", SqlDbType.Int).Value = schedule == "same-quote" ? garages : 1;
                command.ExecuteScalar(); command.Parameters.Clear();
            }
            command.CommandText = "UPDATE VehicleInvoice SET Comment='pending'" + (schedule == "invoice-write" ? "" : ",WorkQuoteId=@quote") + " WHERE Id=@invoice";
            command.Parameters.Add("@invoice", SqlDbType.Int).Value = invoice;
            if (schedule != "invoice-write") command.Parameters.Add("@quote", SqlDbType.Int).Value = quote;
            command.ExecuteNonQuery(); command.Parameters.Clear();
            if (schedule == "invoice-and-join-write")
            {
                command.CommandText = "INSERT InvoiceWorkQuote(GarageBusinessCustomerId,InvoiceId,WorkQuoteId,CreatedDate,CreatedBy) VALUES(1,@invoice,@quote,'20260101','experiment')";
                command.Parameters.Add("@invoice", SqlDbType.Int).Value = invoice;
                command.Parameters.Add("@quote", SqlDbType.Int).Value = quote; command.ExecuteNonQuery();
            }
        }
        var trace = new Trace(); var worker = database.Track(Task.Run(() =>
        {
            using var context = Context(database, trace); var timer = Stopwatch.StartNew();
            var result = new VehicleInvoiceService(context).AddOrUpdateVehicleInvoice(Input(garages, garages * 1000 + 4, garages * 1000 + 2), garages, "experiment");
            return (result.Success, result.ErrorMessage, elapsed: timer.Elapsed.TotalMilliseconds);
        }));
        List<string[]> waits = new(); var beforeRelease = false;
        try
        {
            using var diagnostic = database.OpenOwnedConnection();
            for (var attempt = 0; attempt < 20; attempt++)
            {
                if (worker.IsCompleted) break;
                await Task.Delay(50);
                var spid = Volatile.Read(ref trace.Spid);
                if (spid == 0) continue;
                using var command = diagnostic.CreateCommand(); command.CommandTimeout = 3;
                command.CommandText = @"SELECT r.session_id,r.blocking_session_id,r.wait_type,r.wait_time,r.wait_resource,r.status,
                    SUBSTRING(t.text,r.statement_start_offset/2+1,(CASE WHEN r.statement_end_offset=-1 THEN DATALENGTH(t.text) ELSE r.statement_end_offset END-r.statement_start_offset)/2+1)
                    FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
                    WHERE r.database_id=DB_ID() AND r.session_id=@reader AND r.blocking_session_id=@writer";
                command.Parameters.Add("@reader", SqlDbType.Int).Value = spid;
                command.Parameters.Add("@writer", SqlDbType.Int).Value = writerSpid;
                using var reader = command.ExecuteReader();
                while (reader.Read()) waits.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "<NULL>" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)!).ToArray());
                if (waits.Count > 0) break;
            }
            beforeRelease = worker.IsCompleted;
        }
        finally
        {
            // Same-quote winner commits its attachment; unrelated writers roll
            // back so the next stage/schedule starts with the same graph.
            if (schedule == "same-quote") transaction.Commit(); else transaction.Rollback();
            await database.DrainAsync();
        }
        var writerDuration = clock.Elapsed.TotalMilliseconds;
        var result = await worker.WaitAsync(TimeSpan.FromSeconds(10));
        if (schedule == "same-quote")
        {
            Assert.IsFalse(beforeRelease); Assert.IsFalse(result.Success); Assert.IsTrue(waits.Count > 0, "Missing same-record wait evidence.");
            using var resetWinner = database.OpenOwnedConnection();
            Execute(resetWinner, $"UPDATE VehicleInvoice SET WorkQuoteId=NULL,Comment='seed' WHERE Id={invoice}");
        }
        else
        {
            Assert.IsTrue(result.Success, result.ErrorMessage);
            if (stage == "baseline") Assert.IsTrue(waits.Count > 0, "STOP: baseline cross-garage wait was not diagnosed.");
        }
        using var reset = database.OpenOwnedConnection(); ResetProbe(reset, garages);
        return new { stage, garages, kind = "concurrency", schedule, writerSpid,
            readerSpid = trace.Spid, progressedBeforeRelease = beforeRelease, waits, writerTransactionAndDrainMilliseconds = writerDuration,
            readerMilliseconds = result.elapsed, result.Success, result.ErrorMessage, commands = trace.Commands.ToArray() };
    }

    private static object WriteCosts(DisposableSqlDatabase database, string stage, int garages)
    {
        using var connection = database.OpenOwnedConnection(); var trials = new List<object>();
        for (var trial = 0; trial < 5; trial++)
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandTimeout = 8;
            command.CommandText = "CREATE TABLE #writes(Id int PRIMARY KEY,QuoteId int);"; command.ExecuteNonQuery();
            var durations = new List<double>();
            var steps = new[] {
                "INSERT VehicleInvoice(GarageBusinessId,WorkQuoteId,CreatedDate,CreatedBy) OUTPUT inserted.Id,inserted.WorkQuoteId INTO #writes SELECT TOP(100) GarageBusinessCustomerId,Id,'20260101','overhead' FROM WorkQuote ORDER BY Id",
                "UPDATE i SET Comment='overhead update',WorkQuoteId=w.QuoteId FROM VehicleInvoice i JOIN #writes w ON w.Id=i.Id",
                "INSERT InvoiceWorkQuote(GarageBusinessCustomerId,InvoiceId,WorkQuoteId,CreatedDate,CreatedBy) SELECT i.GarageBusinessId,w.Id,w.QuoteId,'20260101','overhead' FROM #writes w JOIN VehicleInvoice i ON i.Id=w.Id",
                "DELETE l FROM InvoiceWorkQuote l JOIN #writes w ON w.Id=l.InvoiceId"
            };
            foreach (var step in steps) { command.CommandText = step; var clock = Stopwatch.StartNew(); command.ExecuteNonQuery(); durations.Add(clock.Elapsed.TotalMilliseconds); }
            transaction.Rollback(); trials.Add(new { trial, invoiceInsert100Ms = durations[0], invoiceUpdate100Ms = durations[1], joinInsert100Ms = durations[2], joinDelete100Ms = durations[3] });
        }
        return new { stage, garages, kind = "write-microbenchmark", note = "Five warm/cold mixed trials, 100 rows per operation, one rolled-back transaction. Raw table/index costs, not end-to-end service latency. Identity gaps and allocation/cache effects are possible; not a production throughput estimate.", trials };
    }

    private static object Correctness(DisposableSqlDatabase database, string stage, int garages)
    {
        var trace = new Trace(); var assertions = new List<string>();
        using var context = Context(database, trace); var service = new VehicleInvoiceService(context);
        var linkedId = garages * 1000 + 1; var quote = garages * 1000 + 2;
        Assert.IsFalse(service.DeleteVehicleInvoice(linkedId, garages).Success); assertions.Add("mixed linked invoice deletion rejected");
        var first = service.AddOrUpdateVehicleInvoice(Input(garages, garages * 1000 + 4, quote), garages, "experiment");
        Assert.IsTrue(first.Success, first.ErrorMessage);
        Assert.IsFalse(service.CreateFromWorkQuote(quote, garages, "experiment").Success); assertions.Add("direct-only attachment prevents conversion");
        using (var stale = Context(database, new Trace()))
            Assert.IsFalse(new VehicleInvoiceService(stale).AddOrUpdateVehicleInvoice(Input(garages, garages * 1000 + 4, null), garages, "stale").Success);
        assertions.Add("stale null relationship overwrite rejected");
        Assert.IsFalse(service.DeleteVehicleInvoice(garages * 1000 + 4, garages).Success); assertions.Add("direct-only linked invoice deletion rejected");
        using var connection = database.OpenOwnedConnection(); ResetProbe(connection, garages);
        Execute(connection, $"INSERT InvoiceWorkQuote(GarageBusinessCustomerId,InvoiceId,WorkQuoteId,CreatedDate) VALUES(1,-123,{quote},'20260101')");
        using (var malformed = Context(database, new Trace()))
            Assert.IsFalse(new VehicleInvoiceService(malformed).AddOrUpdateVehicleInvoice(Input(garages, garages * 1000 + 4, quote), garages, "experiment").Success);
        assertions.Add("foreign-labelled missing-endpoint association fails closed");
        Execute(connection, $"DELETE InvoiceWorkQuote WHERE InvoiceId=-123 AND WorkQuoteId={quote}");
        Assert.AreEqual("<NULL>", Rows(connection, $"SELECT WorkQuoteId FROM VehicleInvoice WHERE Id={garages * 1000 + 4}").Single()[0]);
        return new { stage, garages, kind = "correctness", assertions };
    }
}

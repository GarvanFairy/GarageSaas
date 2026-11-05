using System.Data.Common;
using GarageSaas.Models;
using GarageSaas.Services;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass, DoNotParallelize, TestCategory("SqlServer")]
public class EfUpgradeWorkflowSqlTests
{
    [TestMethod]
    public async Task CustomerThroughPaidInvoiceAndHistoryUsesSqlServerWithoutSchemaChanges()
    {
        var database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_EF_UPGRADE_SQL_TESTS", "QuoteConversion");
        database.Create();
        try
        {
            // The option must survive schema initialization and closed setup
            // connections. Read metadata only through our ownership-checked API.
            using (var owned = database.OpenOwnedConnection())
            using (var lifecycle = owned.CreateCommand())
            {
                lifecycle.CommandTimeout = 8;
                lifecycle.CommandText = "SELECT is_auto_close_on FROM sys.databases WHERE database_id=DB_ID()";
                Assert.AreEqual(false, lifecycle.ExecuteScalar());
            }
            var commands = new Commands();
            var options = new DbContextOptionsBuilder<SignupContext>(database.Options()).AddInterceptors(commands).Options;
            using var db = new SignupContext(options);
            var graph = SqlInvoiceGraph.Seed(db);
            var other = SqlInvoiceGraph.Seed(db);
            var customer = new GarageBusinessCustomer { GarageCustomerForename = "Upgrade", GarageCustomerSurname = "Customer" };
            Assert.IsTrue(new GarageCustomersService(db, null).AddOrUpdateGarageCustomer(customer, graph.GarageId, "test").Success);
            customer = db.GarageBusinessCustomer.Single(x => x.GarageBusinessId == graph.GarageId && x.GarageCustomerForename == "Upgrade");
            var vehicle = new CustomerVehicle { GarageOwned = false,  VehicleRegistration = "UPGRADE TEST", VehicleMileageDate = DateTime.Today };
            Assert.IsTrue(new CustomerVehicleService(db, null).AddOrUpdateCustomerVehicle(new VehicleAndCustomers
            {
                Vehicle = vehicle, GarageVehicleOwnerListItem = new SelectListItem { Value = customer.Id.ToString() }
            }, graph.GarageId, "test").Success);
            vehicle = db.CustomerVehicle.Single(x => x.GarageBusinessId == graph.GarageId && x.VehicleRegistration == "UPGRADE TEST");
            var work = new WorkItemService(db).AddOrUpdateWorkItem(new WorkItem
            {
                CustomerId = customer.Id, VehicleId = vehicle.Id, RepairInstructions = "Replace brake pads"
            }, graph.GarageId, "test");
            Assert.IsTrue(work.Success, work.ErrorMessage);
            var quote = new WorkQuoteService(db).AddOrUpdateWorkQuote(new CombinedWorkQuoteWorkitem
            {
                CustomerId = customer.Id, VehicleId = vehicle.Id, WorkQuoteDate = DateTime.Today,
                WorkItemIds = new List<int> { work.Data.Id }, Labour = 100m
            }, graph.GarageId, "test");
            Assert.IsTrue(quote.Success, quote.ErrorMessage);
            var invoices = new VehicleInvoiceService(db);
            var invoice = invoices.CreateFromWorkQuote(quote.Data.WorkQuoteId, graph.GarageId, "test");
            Assert.IsTrue(invoice.Success, invoice.ErrorMessage);
            Assert.AreEqual(123.50m, invoice.Data.Total);
            Assert.IsTrue(invoices.MarkInvoiceAsPaid(invoice.Data.Id, graph.GarageId, "test").Success);
            Assert.IsFalse(invoices.MarkInvoiceAsPaid(invoice.Data.Id, other.GarageId, "test").Success);
            using var fresh = new SignupContext(options);
            var saved = fresh.VehicleInvoice.Single(x => x.Id == invoice.Data.Id);
            Assert.AreEqual(123.50m, saved.Total);
            Assert.AreEqual("Paid", saved.InvoiceStatus);
            Assert.IsTrue(saved.Paid == true);
            Assert.IsNotNull(saved.PaidDate);
            var history = CoreGarageWorkflowTests.History(fresh).Get(vehicle.Id, graph.GarageId);
            Assert.IsTrue(history.Success);
            Assert.AreEqual("Replace brake pads", history.Data.Entries.Single().WorkItems.Single().RepairInstructions);
            Assert.IsFalse(CoreGarageWorkflowTests.History(fresh).Get(vehicle.Id, other.GarageId).Success);
            Assert.IsTrue(new GarageCustomersService(fresh, null).GetGarageCustomersForList(graph.GarageId).Data.Any(x => x.VehicleId == vehicle.Id));
            Assert.IsTrue(new CustomerVehicleService(fresh, null).GetCustomerVehiclesForList(graph.GarageId).Data.Any(x => x.VehicleId == vehicle.Id));
            Assert.IsFalse(fresh.ChangeTracker.HasChanges());
            Assert.IsTrue(commands.Sql.Any(x => x.Contains("OUTPUT INSERTED", StringComparison.OrdinalIgnoreCase)), "Exercise EF 10 SQL Server OUTPUT writes.");
            Assert.IsTrue(commands.Sql.Any(x => x.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase)), "Retain existing lock queries.");
            var connection = new SqlConnectionStringBuilder(database.ConnectionString);
            Assert.AreNotEqual(SqlConnectionEncryptOption.Optional, connection.Encrypt, "New SQL client requests encryption by default.");
            Assert.IsTrue(connection.TrustServerCertificate, "Certificate trust bypass is limited to the disposable local fixture.");
        }
        finally { await database.DeleteAsync(); }
    }

    private sealed class Commands : DbCommandInterceptor
    {
        internal List<string> Sql { get; } = new();
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        { Sql.Add(command.CommandText); return result; }
    }
}

using GarageSaas.Services;
using GarageSaas.Services.Models;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

// Re-entrant SaveChanges gates demonstrate service ordering only. They do NOT
// establish SQL locking, isolation or transaction rollback; see opt-in SQL fixture.
[TestClass]
public class WorkItemQuoteLinkInterleavingTests
{
    [DataTestMethod]
    [DataRow(false, false)] [DataRow(false, true)]
    [DataRow(true, false)] [DataRow(true, true)]
    public void GarageConsistentRetargetAndLinkBothPassWhenValidationIsInterleaved(bool update, bool quoteFirst)
    {
        var options = new DbContextOptionsBuilder<SignupContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        using var seed = new SignupContext(options);
        var graph = SqlInvoiceGraph.Seed(seed);
        var replacement = new CustomerVehicle { GarageBusinessId = graph.GarageId, GarageOwned = true,
            CreatedDate = DateTime.UtcNow, VehicleMileageDate = DateTime.UtcNow };
        seed.CustomerVehicle.Add(replacement); seed.SaveChanges();
        CombinedWorkQuoteWorkitem Input(int id = 0, int? item = null) => new()
        {
            WorkQuoteId = id, VehicleId = graph.VehicleId, CustomerId = graph.CustomerId,
            WorkItemIds = new() { item ?? graph.WorkItemId }, WorkQuoteDate = DateTime.Today
        };
        WorkItem Retarget() => new() { Id = graph.WorkItemId, VehicleId = replacement.Id,
            CustomerId = graph.CustomerId, RepairInstructions = "retarget" };
        var quoteId = 0;
        if (update)
        {
            var spare = new WorkItem { GarageBusinessCustomerId = graph.GarageId,
                VehicleId = graph.VehicleId, CustomerId = graph.CustomerId, CreatedDate = DateTime.UtcNow };
            seed.WorkItem.Add(spare); seed.SaveChanges();
            quoteId = new WorkQuoteService(seed).AddOrUpdateWorkQuote(Input(item: spare.Id), graph.GarageId, "seed").Data.WorkQuoteId;
        }
        using var first = new GateContext(options) { GateSave = quoteFirst && !update ? 2 : 1 };
        first.BeforeSave = () =>
        {
            using var other = new SignupContext(options);
            if (quoteFirst)
                Assert.IsTrue(new WorkItemService(other).AddOrUpdateWorkItem(Retarget(), graph.GarageId, "second").Success);
            else
                Assert.IsTrue(new WorkQuoteService(other).AddOrUpdateWorkQuote(Input(quoteId), graph.GarageId, "second").Success);
        };
        ServiceResult result = quoteFirst
            ? new WorkQuoteService(first).AddOrUpdateWorkQuote(Input(quoteId), graph.GarageId, "first")
            : new WorkItemService(first).AddOrUpdateWorkItem(Retarget(), graph.GarageId, "first");
        Assert.IsTrue(result.Success, result.ErrorMessage);
        using var check = new SignupContext(options);
        var item = check.WorkItem.Single(x => x.Id == graph.WorkItemId);
        var link = check.WorkQuoteWorkItem.Single(x => x.WorkItemId == item.Id);
        var quote = check.WorkQuote.Single(x => x.Id == link.WorkQuoteId);
        Assert.AreEqual(replacement.Id, item.VehicleId);
        Assert.AreEqual(graph.VehicleId, quote.VehicleId);
        Assert.IsTrue(new WorkQuoteService(check).GetWorkQuote(quote.Id, graph.GarageId).Success);
    }

    private sealed class GateContext : SignupContext
    {
        public int GateSave, Saves;
        public Action? BeforeSave;
        public GateContext(DbContextOptions<SignupContext> options) : base(options) { }
        public override int SaveChanges()
        {
            Saves++; if (Saves == GateSave) BeforeSave?.Invoke(); return base.SaveChanges();
        }
    }
}

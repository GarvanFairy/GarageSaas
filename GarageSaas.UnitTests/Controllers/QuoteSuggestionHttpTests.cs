using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;
using static GarageSaas.UnitTests.Controllers.GarageSwitchingHttpTests;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class QuoteSuggestionHttpTests
{
    [TestMethod]
    public async Task GarageLimitUsesServerSelectionAndDoesNotSpendAnotherGaragesAllowance()
    {
        var provider = Provider();
        await using var h = await SwitchHost.Start(suggestions: provider.Object, configureAi: o => o.GarageRequestsPerMinute = 1);
        await h.Select(1); var context = await Context(h);
        using var allowed = await h.Json("/QuoteSuggestion/Suggest", new { description = "Replace pads", garageId = 2 }, context);
        Assert.AreEqual(HttpStatusCode.OK, allowed.StatusCode);
        using var limited = await h.Json("/QuoteSuggestion/Suggest", new { description = "Replace pads", garageId = 2 }, context);
        Assert.AreEqual(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.AreEqual(1L, h.AiUsage.Snapshot(1).Limited); Assert.AreEqual(0L, h.AiUsage.Snapshot(2).Started);
        await h.Select(2); context = await Context(h);
        using var other = await h.Json("/QuoteSuggestion/Suggest", new { description = "Change oil", garageId = 1 }, context);
        Assert.AreEqual(HttpStatusCode.OK, other.StatusCode);
        Assert.AreEqual(1L, h.AiUsage.Snapshot(2).Started);
        provider.Verify(p => p.SuggestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task SelectedGarageControlsUsageAndResponsesDespiteSpoofedClientIds()
    {
        var provider = Provider();
        provider.Setup(p => p.SuggestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string draft, CancellationToken _) => Task.FromResult(draft == "Replace pads"
                ? QuoteSuggestionResult.Ok("Pads suggestion only.").WithUsage(11, 3)
                : QuoteSuggestionResult.Ok("Oil suggestion only.").WithUsage(22, 4)));
        await using var h = await SwitchHost.Start(suggestions: provider.Object); await h.Select(1); Seed(h);
        var firstContext = await Context(h);
        using var first = await h.Json("/QuoteSuggestion/Suggest", new { description = "Replace pads", workQuoteId = 100, garageId = 2, userId = 999 }, firstContext);
        var firstBody = await first.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode); StringAssert.Contains(firstBody, "Pads suggestion");
        Assert.IsFalse(firstBody.Contains("inputTokens")); Assert.IsFalse(firstBody.Contains("garageId"));
        await h.Select(2); var secondContext = await Context(h);
        using var second = await h.Json("/QuoteSuggestion/Suggest", new { description = "Change oil", workQuoteId = 200, garageId = 1 }, secondContext);
        var secondBody = await second.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, second.StatusCode); StringAssert.Contains(secondBody, "Oil suggestion");
        Assert.IsFalse(secondBody.Contains("Pads suggestion"));
        Assert.AreEqual(11L, h.AiUsage.Snapshot(1).InputTokens); Assert.AreEqual(22L, h.AiUsage.Snapshot(2).InputTokens);
        Assert.AreEqual(1L, h.AiUsage.Snapshot(1).Succeeded); Assert.AreEqual(1L, h.AiUsage.Snapshot(2).Succeeded);
        Assert.AreEqual(0L, h.AiUsage.Snapshot(999).Started);
        using var foreign = await h.Json("/QuoteSuggestion/Suggest", new { description = "Change oil", workQuoteId = 100 }, secondContext);
        Assert.AreEqual(HttpStatusCode.NotFound, foreign.StatusCode);
        using var stale = await h.Json("/QuoteSuggestion/Suggest", new { description = "Change oil" }, firstContext);
        Assert.AreEqual(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.AreEqual(1L, h.AiUsage.Snapshot(2).Started);
        provider.Verify(p => p.SuggestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task UnexpectedProviderExceptionsAreSanitizedAndFailureUsageIsTenantScoped()
    {
        var provider = Provider();
        provider.Setup(p => p.SuggestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Private content from garage one"));
        await using var h = await SwitchHost.Start(suggestions: provider.Object); await h.Select(2);
        var context = await Context(h);
        using var response = await h.Json("/QuoteSuggestion/Suggest", new { description = "Change oil", garageId = 1 }, context);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("Private"));
        Assert.AreEqual(1L, h.AiUsage.Snapshot(2).Failed); Assert.AreEqual(0L, h.AiUsage.Snapshot(1).Started);
    }

    [TestMethod]
    public async Task MissingRealServiceKeyLeavesEditorAvailableWithoutAnyOutboundCall()
    {
        var handler = new GarageSaas.UnitTests.Services.QuoteSuggestionServiceTests.Handler("must not be called");
        var service = new GarageSaas.Services.OpenAIQuoteSuggestionService(new HttpClient(handler),
            Microsoft.Extensions.Options.Options.Create(new GarageSaas.Configuration.OpenAIOptions()));
        await using var h = await SwitchHost.Start(suggestions: service); await h.Select(1);
        var context = await Context(h);
        using var response = await h.Json("/QuoteSuggestion/Suggest", new { description = "Replace pads" }, context);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual(0, handler.Calls);
        using var editor = await h.Send("GET", "/WorkQuote/AddEdit");
        Assert.AreEqual(HttpStatusCode.OK, editor.StatusCode);
    }

    private static Mock<IQuoteSuggestionService> Provider()
    {
        var provider=new Mock<IQuoteSuggestionService>();provider.Setup(p=>p.SuggestAsync(It.IsAny<string>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuoteSuggestionResult.Ok("Replace the front brake pads."));return provider;
    }
    private static async Task<string> Context(SwitchHost h)
    {
        using var page=await h.Send("GET","/WorkQuote/AddEdit");Assert.AreEqual(HttpStatusCode.OK,page.StatusCode);
        var html=await page.Content.ReadAsStringAsync();StringAssert.Contains(html,"Suggest Description");StringAssert.Contains(html,"Use Suggestion");
        return WebUtility.HtmlDecode(Regex.Match(html,"name=\"garage-context\" content=\"([^\"]+)\"").Groups[1].Value);
    }
    private static void Seed(SwitchHost h)
    {
        using var db=new SignupContext(h.Options);
        db.Add(new WorkQuote { Id=100,GarageBusinessCustomerId=1,CustomerId=1,WorkRequest="Original work",Labour=100,Total=123.5m });
        db.Add(new WorkQuote { Id=200,GarageBusinessCustomerId=2,CustomerId=2,WorkRequest="Private work" });db.SaveChanges();
    }
    [DataTestMethod] [DataRow(0)] [DataRow(100)]
    public async Task NewAndExistingQuotesReturnSuggestionWithoutPersistence(int quoteId)
    {
        var provider=Provider();await using var h=await SwitchHost.Start(suggestions:provider.Object);await h.Select(1);Seed(h);var context=await Context(h);
        using var response=await h.Json("/QuoteSuggestion/Suggest",new{description="  Replace front pads  ",workQuoteId=quoteId,garageId=2},context);
        Assert.AreEqual(HttpStatusCode.OK,response.StatusCode);Assert.IsTrue(response.Headers.CacheControl!.NoStore);
        var result=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.AreEqual("Replace the front brake pads.",result.GetProperty("data").GetProperty("suggestion").GetString());
        provider.Verify(p=>p.SuggestAsync("Replace front pads",It.IsAny<CancellationToken>()),Times.Once);
        using var db=new SignupContext(h.Options);Assert.AreEqual(2,db.WorkQuote.Count());Assert.AreEqual(0,db.WorkItem.Count());
        var quote=db.WorkQuote.Single(q=>q.Id==100);Assert.AreEqual("Original work",quote.WorkRequest);Assert.AreEqual(123.5m,quote.Total);
    }
    [DataTestMethod] [DataRow(200)] [DataRow(999)] [DataRow(-1)]
    public async Task ForeignMissingOrInvalidQuotesNeverReachProvider(int quoteId)
    {
        var provider=Provider();await using var h=await SwitchHost.Start(suggestions:provider.Object);await h.Select(1);Seed(h);var context=await Context(h);
        using var response=await h.Json("/QuoteSuggestion/Suggest",new{description="Replace pads",workQuoteId=quoteId},context);
        Assert.AreEqual(quoteId<0?HttpStatusCode.BadRequest:HttpStatusCode.NotFound,response.StatusCode);provider.VerifyNoOtherCalls();
    }
    [DataTestMethod] [DataRow(null,401)] [DataRow("platform",403)] [DataRow("unknown",403)]
    public async Task AuthenticationAndEligibleMembershipAreRequired(string? identity,int status)
    {
        var provider=Provider();await using var h=await SwitchHost.Start(suggestions:provider.Object);
        using var response=await h.Json("/QuoteSuggestion/Suggest",new{description="Replace pads"},"",identity:identity);
        Assert.AreEqual(status,(int)response.StatusCode);provider.VerifyNoOtherCalls();
    }
    [TestMethod]
    public async Task MissingSelectionOrUnavailableGarageCannotRequestSuggestions()
    {
        var provider=Provider();await using var h=await SwitchHost.Start(suggestions:provider.Object);
        using var unselected=await h.Json("/QuoteSuggestion/Suggest",new{description="Replace pads"},"");Assert.AreEqual(HttpStatusCode.Forbidden,unselected.StatusCode);
        await h.Select(1);var context=await Context(h);
        using(var db=new SignupContext(h.Options)){db.GarageBusiness.Single(g=>g.Id==1).Blocked=true;db.SaveChanges();}
        using var blocked=await h.Json("/QuoteSuggestion/Suggest",new{description="Replace pads"},context);Assert.AreEqual(HttpStatusCode.Forbidden,blocked.StatusCode);provider.VerifyNoOtherCalls();
    }
    [DataTestMethod] [DataRow("missing")] [DataRow("invalid")]
    public async Task AntiforgeryIsRequired(string mode)
    {
        var provider=Provider();await using var h=await SwitchHost.Start(suggestions:provider.Object);await h.Select(1);var context=await Context(h);
        using var response=await h.Json("/QuoteSuggestion/Suggest",new{description="Replace pads"},context,antiforgery:mode);
        Assert.AreEqual(HttpStatusCode.BadRequest,response.StatusCode);provider.VerifyNoOtherCalls();
    }
    [TestMethod]
    public async Task SwitchingGaragesRejectsStaleAjaxAndForeignQuoteIds()
    {
        var provider=Provider();await using var h=await SwitchHost.Start(suggestions:provider.Object);await h.Select(1);Seed(h);var context=await Context(h);await h.Select(2);
        using var stale=await h.Json("/QuoteSuggestion/Suggest",new{description="Replace pads",workQuoteId=100},context);Assert.AreEqual(HttpStatusCode.Conflict,stale.StatusCode);
        var current=await Context(h);using var foreign=await h.Json("/QuoteSuggestion/Suggest",new{description="Replace pads",workQuoteId=100},current);Assert.AreEqual(HttpStatusCode.NotFound,foreign.StatusCode);provider.VerifyNoOtherCalls();
    }
    [DataTestMethod] [DataRow("")] [DataRow("long")] [DataRow("Call me on 0871234567")] [DataRow("Work for CustomerAlpha OnlyA")]
    public async Task InvalidDraftAndKnownCustomerNameAreRejected(string description)
    {
        var provider=Provider();await using var h=await SwitchHost.Start(suggestions:provider.Object);await h.Select(1);Seed(h);var context=await Context(h);
        using var response=await h.Json("/QuoteSuggestion/Suggest",new{description=description=="long"?new string('x',1001):description,workQuoteId=100},context);
        Assert.AreEqual(HttpStatusCode.BadRequest,response.StatusCode);provider.VerifyNoOtherCalls();
    }
    [TestMethod]
    public async Task RequestLimitPreventsAdditionalProviderCalls()
    {
        var provider=Provider();await using var h=await SwitchHost.Start(suggestions:provider.Object);await h.Select(1);var context=await Context(h);
        for(var i=0;i<6;i++){using var response=await h.Json("/QuoteSuggestion/Suggest",new{description="Replace pads"},context);Assert.AreEqual(i<5?HttpStatusCode.OK:HttpStatusCode.TooManyRequests,response.StatusCode);}
        provider.Verify(p=>p.SuggestAsync(It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Exactly(5));
    }
    [TestMethod]
    public async Task UnavailableAiReturnsSafeFeedbackAndManualEditorStillRenders()
    {
        var provider=Provider();provider.Setup(p=>p.SuggestAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(QuoteSuggestionResult.Fail("AiUnavailable","AI unavailable; edit manually."));
        await using var h=await SwitchHost.Start(suggestions:provider.Object);await h.Select(1);var context=await Context(h);
        using var response=await h.Json("/QuoteSuggestion/Suggest",new{description="Replace pads"},context);Assert.AreEqual(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(),"edit manually");using var page=await h.Send("GET","/WorkQuote/AddEdit");Assert.AreEqual(HttpStatusCode.OK,page.StatusCode);
    }
}

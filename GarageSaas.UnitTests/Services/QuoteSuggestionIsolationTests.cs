using System.Net;
using System.Text;
using System.Text.Json;
using GarageSaas.Configuration;
using GarageSaas.Services;
using GarageSaas.Services.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class QuoteSuggestionIsolationTests
{
    [TestMethod]
    public void GarageCapacityIsSharedByItsUsersAndIndependentOfOtherGarages()
    {
        using var cache=new MemoryCache(new MemoryCacheOptions());
        var limits=new QuoteSuggestionRateLimiter(cache,Options.Create(new OpenAIOptions { RequestsPerMinute=5,GarageRequestsPerMinute=2 }));
        Assert.IsTrue(limits.TryAcquire(11,1));Assert.IsTrue(limits.TryAcquire(12,1));
        Assert.IsFalse(limits.TryAcquire(13,1),"All users of a garage share its allowance.");
        Assert.IsTrue(limits.TryAcquire(13,2),"A denied attempt must not consume another garage's or user's allowance.");
        Assert.IsFalse(limits.TryAcquire(13,0));
    }
    [TestMethod]
    public void PerUserLimitIsRetainedAlongsideGarageLimit()
    {
        using var cache=new MemoryCache(new MemoryCacheOptions());
        var limits=new QuoteSuggestionRateLimiter(cache,Options.Create(new OpenAIOptions { RequestsPerMinute=1,GarageRequestsPerMinute=20 }));
        Assert.IsTrue(limits.TryAcquire(11,1));Assert.IsFalse(limits.TryAcquire(11,2));Assert.IsTrue(limits.TryAcquire(12,2));
    }
    [TestMethod]
    public void UsageSnapshotsContainOnlyIndependentNumericCountersAndCannotModifyStoredMetrics()
    {
        using var cache=new MemoryCache(new MemoryCacheOptions());var usage=new QuoteSuggestionUsage(cache);
        usage.Started(1);usage.Completed(1,QuoteSuggestionResult.Ok("Private garage one wording").WithUsage(20,10));
        usage.Started(2);usage.Completed(2,QuoteSuggestionResult.Fail("AiUnavailable","Private provider detail").WithUsage(40,15));usage.Limited(2);
        var one=usage.Snapshot(1);var two=usage.Snapshot(2);
        Assert.AreEqual(1L,one.Succeeded);Assert.AreEqual(0L,one.Failed);Assert.AreEqual(20L,one.InputTokens);Assert.AreEqual(10L,one.OutputTokens);Assert.AreEqual(0L,one.Limited);
        Assert.AreEqual(0L,two.Succeeded);Assert.AreEqual(1L,two.Failed);Assert.AreEqual(40L,two.InputTokens);Assert.AreEqual(15L,two.OutputTokens);Assert.AreEqual(1L,two.Limited);
        Assert.AreEqual(0L,usage.Snapshot(3).Started);
        var changed=one with { InputTokens=999 };Assert.AreEqual(20L,usage.Snapshot(1).InputTokens);
        Assert.IsTrue(typeof(QuoteSuggestionUsage.UsageSnapshot).GetProperties().All(p=>p.PropertyType==typeof(int)||p.PropertyType==typeof(long)));
        Assert.IsFalse(JsonSerializer.Serialize(one).Contains("Private"));
    }
    [TestMethod]
    public async Task ReusedSharedKeyClientSendsOnlyCurrentDraftWithoutConversationOrSuggestionCache()
    {
        var handler=new SequenceHandler();var service=new OpenAIQuoteSuggestionService(new HttpClient(handler),Options.Create(new OpenAIOptions {ApiKey="shared-synthetic-key"}));
        var first=await service.SuggestAsync("Replace front pads",CancellationToken.None);
        var second=await service.SuggestAsync("Change engine oil",CancellationToken.None);
        var third=await service.SuggestAsync("Replace front pads",CancellationToken.None);
        Assert.AreEqual(3,handler.Bodies.Count);Assert.AreNotEqual(first.Suggestion,second.Suggestion);Assert.AreNotEqual(first.Suggestion,third.Suggestion,"Even identical drafts generate their own response.");
        for(var i=0;i<3;i++)
        {
            using var json=JsonDocument.Parse(handler.Bodies[i]);var body=json.RootElement;
            Assert.IsFalse(body.GetProperty("store").GetBoolean());Assert.AreEqual(1,body.GetProperty("input").GetArrayLength());
            Assert.AreEqual(i==1?"Change engine oil":"Replace front pads",body.GetProperty("input")[0].GetProperty("content")[0].GetProperty("text").GetString());
            foreach(var forbidden in new[]{"previous_response_id","conversation","metadata","user","assistant_id","thread_id","garageId"})Assert.IsFalse(body.TryGetProperty(forbidden,out _));
            Assert.IsFalse(handler.Bodies[i].Contains("Suggested wording"));Assert.AreEqual("Bearer shared-synthetic-key",handler.Authorizations[i]);
        }
        Assert.AreEqual(31L,first.InputTokens);Assert.AreEqual(8L,first.OutputTokens);
    }
    [TestMethod]
    public async Task IncompleteResponseRetainsReportedUsageWithoutReturningItsText()
    {
        var handler=new SequenceHandler { Incomplete=true };var service=new OpenAIQuoteSuggestionService(new HttpClient(handler),Options.Create(new OpenAIOptions {ApiKey="shared-synthetic-key"}));
        var result=await service.SuggestAsync("Replace pads",CancellationToken.None);
        Assert.IsFalse(result.Success);Assert.IsNull(result.Suggestion);Assert.AreEqual(31L,result.InputTokens);Assert.AreEqual(8L,result.OutputTokens);
    }
    private sealed class SequenceHandler:HttpMessageHandler
    {
        public List<string> Bodies=new();public List<string?> Authorizations=new();public bool Incomplete;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(token));Authorizations.Add(request.Headers.Authorization?.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new {
                status=Incomplete?"incomplete":"completed",usage=new{input_tokens=30+Bodies.Count,output_tokens=8},
                output=new[]{new{type="message",role="assistant",status="completed",content=new[]{new{type="output_text",text="Suggested wording "+Bodies.Count}}}}
            }),Encoding.UTF8,"application/json")};
        }
    }
}

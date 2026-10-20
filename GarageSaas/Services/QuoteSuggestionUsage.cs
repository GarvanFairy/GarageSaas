using System;
using GarageSaas.Services.Models;
using Microsoft.Extensions.Caching.Memory;

namespace GarageSaas.Services
{
    // Per-process numeric pilot counters. Never store drafts, suggestions or provider response IDs.
    // No HTTP metrics endpoint: snapshots are for internal diagnostics and tests only.
    public sealed class QuoteSuggestionUsage
    {
        private readonly IMemoryCache _cache;
        private readonly object _gate = new object();
        public QuoteSuggestionUsage(IMemoryCache cache) { _cache = cache; }
        public void Started(int garageId) { lock (_gate) CounterFor(garageId).Started++; }
        public void Limited(int garageId) { lock (_gate) CounterFor(garageId).Limited++; }
        public void Completed(int garageId, QuoteSuggestionResult result)
        {
            lock (_gate)
            {
                var counter = CounterFor(garageId);
                if (result.Success) counter.Succeeded++; else counter.Failed++;
                counter.InputTokens += result.InputTokens ?? 0;
                counter.OutputTokens += result.OutputTokens ?? 0;
                if (result.InputTokens.HasValue || result.OutputTokens.HasValue) counter.WithReportedUsage++;
            }
        }
        public UsageSnapshot Snapshot(int garageId)
        {
            lock (_gate)
            {
                _cache.TryGetValue(Key(garageId), out Counter counter);
                return new UsageSnapshot(garageId, counter?.Started ?? 0, counter?.Succeeded ?? 0,
                    counter?.Failed ?? 0, counter?.Limited ?? 0, counter?.InputTokens ?? 0,
                    counter?.OutputTokens ?? 0, counter?.WithReportedUsage ?? 0);
            }
        }
        private Counter CounterFor(int garageId)
        {
            if (garageId <= 0) throw new ArgumentOutOfRangeException(nameof(garageId));
            if (!_cache.TryGetValue(Key(garageId), out Counter counter))
                _cache.Set(Key(garageId), counter = new Counter(), TimeSpan.FromHours(24));
            return counter;
        }
        private static string Key(int garageId) => "quote-ai-usage:garage:" + garageId;
        private sealed class Counter
        {
            public long Started, Succeeded, Failed, Limited, InputTokens, OutputTokens, WithReportedUsage;
        }
        public sealed record UsageSnapshot(int GarageId, long Started, long Succeeded, long Failed,
            long Limited, long InputTokens, long OutputTokens, long WithReportedUsage);
    }
}

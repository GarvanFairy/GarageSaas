using System;
using GarageSaas.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GarageSaas.Services
{
    public sealed class QuoteSuggestionRateLimiter
    {
        private readonly IMemoryCache _cache;
        private readonly OpenAIOptions _options;
        private readonly object _gate = new object();
        public QuoteSuggestionRateLimiter(IMemoryCache cache, IOptions<OpenAIOptions> options) { _cache = cache; _options = options.Value; }
        public bool TryAcquire(int userId, int garageId)
        {
            if (userId <= 0 || garageId <= 0 || !_options.ValidLimits) return false;
            lock (_gate)
            {
                var user = CounterFor("quote-ai-rate:user:" + userId);
                var garage = CounterFor("quote-ai-rate:garage:" + garageId);
                if (user.Count >= _options.RequestsPerMinute || garage.Count >= _options.GarageRequestsPerMinute) return false;
                user.Count++;
                garage.Count++;
                return true;
            }
        }
        private Counter CounterFor(string key)
        {
            if (!_cache.TryGetValue(key, out Counter counter))
                _cache.Set(key, counter = new Counter(), TimeSpan.FromMinutes(1));
            return counter;
        }
        private sealed class Counter { public int Count; }
    }
}

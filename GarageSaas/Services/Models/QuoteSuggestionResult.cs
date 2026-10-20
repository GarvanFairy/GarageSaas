namespace GarageSaas.Services.Models
{
    public class QuoteSuggestionRequest
    {
        public string Description { get; set; }
        public int WorkQuoteId { get; set; }
    }

    public class QuoteSuggestionResult
    {
        public bool Success => Suggestion != null;
        public string Suggestion { get; private set; }
        public string Code { get; private set; }
        public string Message { get; private set; }
        public int StatusCode { get; private set; }
        // Internal accounting only: the controller never serializes these fields.
        public long? InputTokens { get; private set; }
        public long? OutputTokens { get; private set; }
        public QuoteSuggestionResult WithUsage(long? inputTokens, long? outputTokens)
        {
            InputTokens = inputTokens >= 0 ? inputTokens : null;
            OutputTokens = outputTokens >= 0 ? outputTokens : null;
            return this;
        }
        public static QuoteSuggestionResult Ok(string text) => new QuoteSuggestionResult { Suggestion = text, StatusCode = 200 };
        public static QuoteSuggestionResult Fail(string code, string message, int status = 503) =>
            new QuoteSuggestionResult { Code = code, Message = message, StatusCode = status };
    }
}

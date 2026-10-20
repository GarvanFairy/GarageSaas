namespace GarageSaas.Configuration
{
    public class OpenAIOptions
    {
        public string ApiKey { get; set; }
        public string Model { get; set; } = "gpt-6-luna";
        public int TimeoutSeconds { get; set; } = 15;
        public int MaxOutputTokens { get; set; } = 200;
        public int MaxInputCharacters { get; set; } = 1000;
        public int MaxSuggestionCharacters { get; set; } = 1000;
        public int RequestsPerMinute { get; set; } = 5;
        public int GarageRequestsPerMinute { get; set; } = 20;
        public bool ValidLimits => TimeoutSeconds >= 1 && TimeoutSeconds <= 60 &&
            MaxOutputTokens >= 16 && MaxOutputTokens <= 1000 &&
            MaxInputCharacters >= 1 && MaxInputCharacters <= 4000 &&
            MaxSuggestionCharacters >= 1 && MaxSuggestionCharacters <= 2000 &&
            RequestsPerMinute >= 1 && RequestsPerMinute <= 20 &&
            GarageRequestsPerMinute >= 1 && GarageRequestsPerMinute <= 1000;
    }
}

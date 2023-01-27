using System.Text.Json.Serialization;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SignupAPI.Models
{
    public class ResponseContent
    {
        public const string ApiVersion = "1.0.0";

        public ResponseContent()
        {
            this.version = ResponseContent.ApiVersion;
            this.action = "Continue";
        }

        public ResponseContent(string action, string userMessage)
        {
            this.version = ResponseContent.ApiVersion;
            this.action = action;
            this.userMessage = userMessage;
            if (action == "ValidationError")
            {
                status = "400";
            }
        }

        [JsonPropertyName("version")]
        public string version { get; }

        [JsonPropertyName("action")]
        public string action { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? status { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? userMessage { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? displayName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? name { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string extension_GarageBusinessName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string extension_GarageBusinessId { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> emailAddresses { get; set; }

       /* [JsonPropertyName("UsersObjectID")]
        public string? UsersObjectID { get; set; } = string.Empty;*/
       

    }
}
    


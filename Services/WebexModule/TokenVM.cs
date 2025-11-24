using System.Text.Json.Serialization;

namespace TASA.Services.WebexModule
{
    public class ErrorVM
    {
        public string Description { set; get; } = string.Empty;
    }
    public class TokenVM
    {
        public string Access_token { set; get; } = string.Empty;
        public int Expires_in { set; get; }
        public string Refresh_token { set; get; } = string.Empty;
        public int Refresh_token_expires_in { set; get; }

        [JsonPropertyName("message")]
        public string? Message { set; get; }

        [JsonPropertyName("errors")]
        public List<ErrorVM>? Errors { set; get; }
    }
}

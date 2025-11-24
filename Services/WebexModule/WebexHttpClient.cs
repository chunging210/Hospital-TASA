using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TASA.Services.WebexModule
{
    public class WebexHttpClient : HttpClient
    {
        private const string WebexApis = "https://webexapis.com";
        public string Token { get; set; } = "MDIwMjk4NzgtMzQ3MC00NTM0LWExMmEtNjU4ZjJmZjUzNTcxN2RkZDJlMTktNTNm_PF84_03f1a28d-93c0-47b4-89b2-fc44b6d50afd";
        private readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public WebexHttpClient()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            BaseAddress = new Uri(WebexApis);
        }

        private HttpRequestMessage CreateRequest(string uri)
        {
            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.RequestUri = new Uri($"{WebexApis}/{uri}");
            return request;
        }

        new private string Send(HttpRequestMessage request)
        {
            var response = SendAsync(request).GetAwaiter().GetResult();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }

        private string AccessToken(Dictionary<string, string> dict)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{WebexApis}/v1/access_token")
            {
                Content = new FormUrlEncodedContent(dict)
            };
            return Send(request);
        }

        public string AuthorizationCode(string clientId, string clientSecret, string code, string host)
        {
            var dict = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "client_id", clientId },
                { "client_secret", clientSecret},
                { "code", code },
                { "redirect_uri", $"{host}/webex" }
            };
            return AccessToken(dict);
        }

        public string RefreshToken(string clientId, string clientSecret, string refreshToken)
        {
            var dict = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", clientId },
                { "client_secret", clientSecret},
                { "refresh_token", refreshToken }
            };
            return AccessToken(dict);
        }

        public record BaseResponse
        {
            public record ErrorVM
            {
                public string Description { get; set; } = string.Empty;
            }
            public string Message { get; set; } = string.Empty;
            public IEnumerable<ErrorVM> Errors { get; set; } = [];
        }

        public record CreateMeetingVM
        {
            public record RequestVM
            {
                public record InviteeVM
                {
                    public string Email { get; set; } = string.Empty;
                    public string DisplayName { get; set; } = string.Empty;
                }
                public string MeetingId { get; set; } = string.Empty;
                public string Title { get; set; } = string.Empty;
                public string Start { get; set; } = string.Empty;
                public string End { get; set; } = string.Empty;
                public string Timezone { get; set; } = string.Empty;
                public bool EnabledAutoRecordMeeting { get; set; } = false;
                public bool EnabledJoinBeforeHost => true;
                public int JoinBeforeHostMinutes { get; set; } = 10;
                public IEnumerable<InviteeVM> Invitees { get; set; } = [];
            }

            public record ResponseVM : BaseResponse
            {
                public record CallInNumberVM
                {
                    public string Label { get; set; } = string.Empty;
                    public string CallInNumber { get; set; } = string.Empty;
                    public string TollType { get; set; } = string.Empty;
                }
                public record TelephonyVM
                {
                    public string AccessCode { get; set; } = string.Empty;
                    public IEnumerable<CallInNumberVM> CallInNumbers { get; set; } = [];
                }
                public string Id { get; set; } = string.Empty;
                public string MeetingNumber { get; set; } = string.Empty;
                public string Password { get; set; } = string.Empty;
                public string MeetingType { get; set; } = string.Empty;
                public string State { get; set; } = string.Empty;
                public bool Adhoc { get; set; }
                public string Timezone { get; set; } = string.Empty;
                public string Start { get; set; } = string.Empty;
                public string End { get; set; } = string.Empty;
                public string Recurrence { get; set; } = string.Empty;
                public string HostUserId { get; set; } = string.Empty;
                public string HostDisplayName { get; set; } = string.Empty;
                public string HostEmail { get; set; } = string.Empty;
                public string HostKey { get; set; } = string.Empty;
                public string SiteUrl { get; set; } = string.Empty;
                public string WebLink { get; set; } = string.Empty;
                public string SipAddress { get; set; } = string.Empty;
                public string DialInIpAddress { get; set; } = string.Empty;
                public string RoomId { get; set; } = string.Empty;
                public bool EnabledAutoRecordMeeting { get; set; }
                public bool AllowAnyUserToBeCoHost { get; set; }
                public bool EnabledJoinBeforeHost { get; set; }
                public bool EnableConnectAudioBeforeHost { get; set; }
                public int JoinBeforeHostMinutes { get; set; }
                public bool ExcludePassword { get; set; }
                public bool PublicMeeting { get; set; }
                public int ReminderTime { get; set; }
                public string UnlockedMeetingJoinSecurity { get; set; } = string.Empty;
                public int SessionTypeId { get; set; }
                public string ScheduledType { get; set; } = string.Empty;
                public bool ExcludePassenabledWebcastViewword { get; set; }
                public string PanelistPassword { get; set; } = string.Empty;
                public TelephonyVM? Telephony { get; set; }
            }
        }

        public string CreateMeeting(CreateMeetingVM.RequestVM vm)
        {
            var json = JsonSerializer.Serialize(vm, Options);
            using var request = CreateRequest("v1/meetings");
            request.Method = HttpMethod.Post;
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return Send(request);
        }

        public string GetMeeting(string meetingId)
        {
            using var request = CreateRequest($"v1/meetings/{meetingId}");
            request.Method = HttpMethod.Get;
            return Send(request);
        }

        public string UpdateMeeting(CreateMeetingVM.RequestVM vm)
        {
            var json = JsonSerializer.Serialize(vm, Options);
            using var request = CreateRequest($"v1/meetings/{vm.MeetingId}");
            request.Method = HttpMethod.Put;
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return Send(request);
        }

        public string DeleteMeeting(string meetingId)
        {
            using var request = CreateRequest($"v1/meetings/{meetingId}");
            request.Method = HttpMethod.Delete;
            return Send(request);
        }

        public record RecordingsVM : BaseResponse
        {
            public record ItemVM
            {
                public string DownloadUrl { get; set; } = string.Empty;
                public string Password { get; set; } = string.Empty;
            }
            public IEnumerable<ItemVM> Items { get; set; } = [];
        }
        public string Recordings(string? meetingId)
        {
            using var request = CreateRequest($"v1/recordings?meetingId={meetingId}");
            request.Method = HttpMethod.Get;
            return Send(request);
        }
    }
}
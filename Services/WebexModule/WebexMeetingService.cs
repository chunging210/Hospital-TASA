using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.WebexModule
{
    public class WebexMeetingService(TASAContext db) : IService
    {
        private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

        /// <summary>
        /// 建立 Webex 會議
        /// ⚠️ 臨時防呆：只支援 StartTime/EndTime 都有值的會議
        /// TODO: 未來支援預約系統後，需要調整邏輯計算時間
        /// </summary>
        public ConferenceWebex? Create(Conference vm, string? meetingId = null)
        {
            // ✅ 防呆：如果 StartTime/EndTime 為 NULL，直接返回 null（不建立 Webex）
            if (vm.StartTime == null || vm.EndTime == null)
            {
                return null;  // 待審核/待繳費的預約不建立 Webex
            }

            if (vm.MCU == 7)
            {
                var invitees = db.AuthUser
                    .AsNoTracking()
                    .WhereNotDeleted()
                    .Where(x => vm.ConferenceUser.Select(y => y.UserId).Contains(x.Id))
                    .Mapping(x => new WebexHttpClient.CreateMeetingVM.RequestVM.InviteeVM()
                    {
                        DisplayName = x.Name
                    });

                var request = new WebexHttpClient.CreateMeetingVM.RequestVM()
                {
                    Title = vm.Name,
                    // ✅ 使用 .Value 因為上面已經檢查 != null
                    Start = vm.StartTime.Value.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    End = vm.EndTime.Value.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    EnabledAutoRecordMeeting = vm.Recording == true,
                    Invitees = invitees
                };

                var usedtime = db.Conference
                    .AsNoTracking()
                    .WhereNotDeleted()
                    .WhereEnabled()
                    // ✅ 修復：檢查 StartTime/EndTime 有值才比較
                    .Where(x => x.Id != vm.Id 
                             && x.MCU == 7 
                             && x.StartTime.HasValue 
                             && x.EndTime.HasValue
                             && x.StartTime < vm.EndTime 
                             && x.EndTime > vm.StartTime)
                    .Select(x => x.ConferenceWebex.WebexId)
                    .ToList();

                var webextoken = db.Webex
                    .AsNoTracking()
                    .WhereNotDeleted()
                    .WhereEnabled()
                    .Where(x => !usedtime.Contains(x.Id))
                    .OrderByDescending(x => Guid.NewGuid())
                    .FirstOrDefault() ?? throw new HttpException("無可用Webex帳號");

                var webecclient = new WebexHttpClient() { Token = webextoken.Access_token };
                var result = "";
                if (string.IsNullOrEmpty(meetingId))
                {
                    result = webecclient.CreateMeeting(request);
                }
                else
                {
                    request.MeetingId = meetingId;
                    result = webecclient.UpdateMeeting(request);
                }

                var response = JsonSerializer.Deserialize<WebexHttpClient.CreateMeetingVM.ResponseVM>(result, CaseInsensitive)!;
                if (string.IsNullOrEmpty(response.Message))
                {
                    return new ConferenceWebex()
                    {
                        ConferencesId = vm.Id,
                        WebexId = webextoken.Id,
                        Id = response.Id,
                        MeetingNumber = response.MeetingNumber,
                        Password = response.Password,
                        CallInNumber = response.Telephony?.CallInNumbers?.FirstOrDefault()?.CallInNumber ?? "",
                        SipAddress = response.SipAddress,
                        HostKey = response.HostKey,
                        WebLink = response.WebLink
                    };
                }
                else
                {
                    throw new HttpException(string.Join(',', response.Errors.Select(x => x.Description)));
                }
            }
            else
            {
                return null;
            }
        }

        //public WebexHttpClient.RecordingsVM? Recording(Guid id)
        //{
        //    var data = Db.Set<Models.Conferences>()
        //        .AsNoTracking()
        //        .Include(x => x.Webex)
        //        .IgnoreDelete()
        //        .FirstOrDefault(x => x.Id == id) ?? throw new ResponseException(I18nMessgae.DataNotFound);

        //    var webecclient = new WebexHttpClient();
        //    var result = webecclient.Recordings(data.Webex.Id);
        //    return JsonSerializer.Deserialize<WebexHttpClient.RecordingsVM>(result, CaseInsensitive);
        //}
    }
}
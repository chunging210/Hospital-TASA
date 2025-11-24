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

        public ConferenceWebex? Create(Conference vm, string? meetingId = null)
        {
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
                    Start = vm.StartTime.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    End = vm.EndTime.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    EnabledAutoRecordMeeting = vm.Recording == true,
                    Invitees = invitees
                };

                var usedtime = db.Conference
                    .AsNoTracking()
                    .WhereNotDeleted()
                    .WhereEnabled()
                    .Where(x => x.Id != vm.Id && x.MCU == 7 && x.StartTime < vm.EndTime && x.EndTime > vm.StartTime)
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

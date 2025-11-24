using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.MailModule
{
    public class ConferenceMail(MailService mailservice, IDbContextFactory<TASAContext> dbContextFactory, IHttpContextAccessor httpContextAccessor) : NUUMail(mailservice, dbContextFactory, httpContextAccessor), IService
    {

        /// <summary>
        /// 會議通知
        /// </summary>
        public void New(Conference conference, string subject = "[新會議通知]", [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            if (!Enable)
            {
                return;
            }

            using var db = dbContextFactory.CreateDbContext();
            db.Conference.Attach(conference);
            db.Entry(conference).Collection(x => x.ConferenceUser).Query().Include(x => x.User).Load();

            var emails = conference.ConferenceUser
                .Where(x => x.User != null && !string.IsNullOrEmpty(x.User.Email))
                .Select(x => new { x.User.Email, x.IsHost })
                .Distinct();
            foreach (var item in emails)
            {
                var mail = NewMailMessage();
                mail.Subject = $"{subject} - {conference.Name}";
                mail.Body = BuildBody(conference, item.IsHost);
                mail.To.Add(item.Email);
                Task.Run(async () =>
                {
                    await SendAsync(mail, className, functionName);
                });
            }
        }

        private static string BuildBody(Conference conference, bool isHost)
        {
            string webexBody = BuildWebex(conference.ConferenceWebex, isHost);
            return $@"<p>會議名稱: {conference.Name}</p>
                <p>會議時間: {TimeFormat(conference.StartTime)} - {TimeFormat(conference.EndTime)}</p>
                <p>會議內容: {conference.Description}</p>
                <p>會議地點: {string.Join(",", conference.Room.Select(x => x.Name))}</p>
                {webexBody}{BuildWebex(conference.ConferenceWebex, isHost)}";
        }

        private static string BuildWebex(ConferenceWebex webex, bool isHost)
        {
            return webex != null ?
                $@"<br/><br/>
                <p>透過會議連結加入</p><p><a href='{webex.WebLink}'>{webex.WebLink}</a></p>
                <br/>
                <p>透過會議號碼加入</p><p>會議號碼：{webex.MeetingNumber}</p><p>會議密碼：{webex.Password}</p>
                <br/>
                <p>透過視訊系統或應用程式加入</p><p><a href='{webex.SipAddress}'>{webex.SipAddress}</a></p>
                {(isHost ? $"<br/><p>主持人金鑰：{webex.HostKey}</p>" : string.Empty)}"
                : string.Empty;
        }

        private static string TimeFormat(DateTime time)
        {
            return time.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
        }
    }
}

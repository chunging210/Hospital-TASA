using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.MailModule
{
    public class PasswordMail(MailService mailservice, IDbContextFactory<TASAContext> dbContextFactory, IHttpContextAccessor httpContextAccessor) : NUUMail(mailservice, dbContextFactory, httpContextAccessor), IService
    {
        /// <summary>
        /// 帳號開通通知信
        /// </summary>
        public void New(string account, string password, string email, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            if (!Enable)
            {
                return;
            }

            var mail = NewMailMessage();
            mail.Subject = "帳號開通通知信";
            mail.Body = $@"
<p>帳號: {account}</p>
<p>密碼: {password}</p>";
            mail.To.Add(email);
            Task.Run(async () =>
            {
                await SendAsync(mail, className, functionName);
            });
        }

        /// <summary>
        /// 密碼重置通知信
        /// </summary>
        public void ReSet(string password, string email, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            if (!Enable)
            {
                return;
            }

            var mail = NewMailMessage();
            mail.Subject = "密碼重置通知信";
            mail.Body = $"<p>密碼: {password}</p>";
            mail.To.Add(email);
            Task.Run(async () =>
            {
                await SendAsync(mail, className, functionName);
            });
        }

        /// <summary>
        /// 忘記密碼通知信
        /// </summary>
        public void Forget(Guid id, DateTime expresson, string email, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            if (!Enable)
            {
                return;
            }

            var mail = NewMailMessage();
            mail.Subject = "忘記密碼通知信";
            var url = $"{BaseUrl}auth/forget?i={id}";
            mail.Body = $@"
<p>您好：</p>
<p>我們收到您的密碼重設請求。</p>
<p>請點擊以下連結，以變更您的密碼：</p>
<p><a href='{url}'>{url}</a></p>
<p>請注意，此連結將在 {expresson:yyyy/MM/dd HH:mm} 後失效。</p>
<p>若您並未提出此請求，請忽略本郵件，您的帳戶密碼將保持不變。</p>";
            mail.To.Add(email);
            Task.Run(async () =>
            {
                await SendAsync(mail, className, functionName);
            });
        }

        /// <summary>
        /// 新使用者註冊通知信（寄給主任/管理者）
        /// </summary>
        public void RegisterNotify(string userName, string userEmail, string departmentName, List<string> directorEmails, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            var validEmails = directorEmails
                .Where(e => !string.IsNullOrWhiteSpace(e) && e.Contains('@'))
                .ToList();

            if (!Enable || validEmails.Count == 0)
            {
                return;
            }

            var mail = NewMailMessage();
            mail.Subject = "新使用者註冊通知";
            mail.Body = $@"
<p>您好：</p>
<p>有新使用者註冊，請至後台審核。</p>
<p>姓名：{userName}</p>
<p>Email：{userEmail}</p>
<p>單位：{departmentName}</p>
<p>請登入管理後台，前往「使用者總覽」進行審核。</p>";
            foreach (var email in validEmails)
            {
                mail.To.Add(email);
            }
            Task.Run(async () =>
            {
                await SendAsync(mail, className, functionName);
            });
        }

        /// <summary>
        /// 帳號審核通過通知信（寄給使用者）
        /// </summary>
        public void AccountApproved(string email, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            if (!Enable)
            {
                return;
            }

            var mail = NewMailMessage();
            mail.Subject = "帳號審核通過通知";
            mail.Body = $@"
<p>您好：</p>
<p>您註冊的台北榮總會議預約系統已通過審核，現在可以登入系統。</p>
<p><a href='{BaseUrl}'>點此前往登入</a></p>";
            mail.To.Add(email);
            Task.Run(async () =>
            {
                await SendAsync(mail, className, functionName);
            });
        }

        /// <summary>
        /// 密碼變更通知信
        /// </summary>
        public void PasswordChange(string email, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            if (!Enable)
            {
                return;
            }

            var mail = NewMailMessage();
            mail.Subject = "密碼變更通知信";
            mail.Body = $@"
<p>您好：</p>
<p>您的密碼已變更。</p>";
            mail.To.Add(email);
            Task.Run(async () =>
            {
                await SendAsync(mail, className, functionName);
            });
        }
    }
}

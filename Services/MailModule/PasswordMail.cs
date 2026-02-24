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
<p>您註冊的臺北榮總會議預約系統已通過審核，現在可以登入系統。</p>
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

        /// <summary>
        /// 帳號審核拒絕通知信（寄給使用者）
        /// </summary>
        public void AccountRejected(string email, string userName, string reason, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            if (!Enable || string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            var mail = NewMailMessage();
            mail.Subject = "帳號審核結果通知";
            mail.Body = $@"
<p>{userName} 您好：</p>
<p>感謝您申請臺北榮總會議預約系統帳號。</p>
<p>經審核後，很抱歉您的申請未通過。</p>
<p><strong>拒絕原因：</strong>{reason}</p>
<p>如有任何疑問，請聯繫系統管理員。</p>";
            mail.To.Add(email);
            Task.Run(async () =>
            {
                await SendAsync(mail, className, functionName);
            });
        }

        /// <summary>
        /// 委派代理人通知信（通知代理人被設定為委派）
        /// </summary>
        public void DelegateAssigned(string delegateEmail, string delegateName, string managerName, DateOnly startDate, DateOnly endDate, List<string> roomNames, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            if (!Enable || string.IsNullOrWhiteSpace(delegateEmail))
            {
                return;
            }

            var roomListHtml = roomNames.Any()
                ? string.Join("", roomNames.Select(r => $"<li>{r}</li>"))
                : "<li>（無會議室資料）</li>";

            var mail = NewMailMessage();
            mail.Subject = "[委派通知] 您已被設定為會議室管理代理人";
            mail.Body = $@"
<h3>{delegateName} 您好：</h3>

<p><strong>{managerName}</strong> 已將您設定為會議室管理代理人。</p>

<h4>📋 委派資訊</h4>
<table style='border-collapse: collapse; width: 100%; max-width: 500px;'>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5; width: 120px;'><strong>委派者</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{managerName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>開始日期</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{startDate:yyyy/MM/dd}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>結束日期</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{endDate:yyyy/MM/dd}</td>
    </tr>
</table>

<h4 style='margin-top: 20px;'>🏢 代理管理的會議室</h4>
<ul>
{roomListHtml}
</ul>

<div style='background-color: #d4edda; border-left: 4px solid #28a745; padding: 15px; margin: 15px 0;'>
    <p style='margin: 0; color: #155724;'>
        在委派期間內，您可以審核上述會議室的預約申請。
    </p>
</div>

<p style='color: #666; margin-top: 30px;'>
    此為系統自動通知，請勿直接回覆此信件。
</p>
";
            mail.To.Add(delegateEmail);
            Task.Run(async () =>
            {
                await SendAsync(mail, className, functionName);
            });
        }

        /// <summary>
        /// 取消委派通知信（通知代理人委派已被取消）
        /// </summary>
        public void DelegateRemoved(string delegateEmail, string delegateName, string managerName, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            if (!Enable || string.IsNullOrWhiteSpace(delegateEmail))
            {
                return;
            }

            var mail = NewMailMessage();
            mail.Subject = "[委派取消通知] 您的代理人身份已被取消";
            mail.Body = $@"
<h3>{delegateName} 您好：</h3>

<p><strong>{managerName}</strong> 已取消您的會議室管理代理人身份。</p>

<div style='background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 15px 0;'>
    <p style='margin: 0; color: #856404;'>
        您將無法再審核該管理者所管理的會議室預約申請。
    </p>
</div>

<p style='color: #666; margin-top: 30px;'>
    此為系統自動通知，請勿直接回覆此信件。
</p>
";
            mail.To.Add(delegateEmail);
            Task.Run(async () =>
            {
                await SendAsync(mail, className, functionName);
            });
        }
    }
}

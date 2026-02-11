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
        /// ⚠️ 臨時防呆：只發送 StartTime/EndTime 都有值的會議
        /// TODO: 未來支援預約系統後，需要調整郵件模板和邏輯
        /// </summary>
        public void New(Conference conference, string subject = "[新會議通知]", [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            if (!Enable)
            {
                return;
            }

            // ✅ 防呆：如果 StartTime/EndTime 為 NULL，不發送郵件（待審核的預約暫不通知）
            if (conference.StartTime == null || conference.EndTime == null)
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
            // ✅ 使用 .Value 因為上面已經檢查 StartTime/EndTime HasValue
            return $@"<p>會議名稱: {conference.Name}</p>
                <p>會議時間: {TimeFormat(conference.StartTime!.Value)} - {TimeFormat(conference.EndTime!.Value)}</p>
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

        /// <summary>
        /// 預約建立通知
        /// 1. 寄給預約者：通知預約內容並請等待審核
        /// 2. 寄給會議室管理者：通知有新預約需要審核
        /// </summary>
        public void ReservationCreated(Guid conferenceId, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            Console.WriteLine($"📧 [ReservationCreated] 開始處理，ConferenceId: {conferenceId}");

            if (!Enable)
            {
                Console.WriteLine("📧 [ReservationCreated] 信件服務未啟用，跳過");
                return;
            }

            using var db = dbContextFactory.CreateDbContext();

            // 取得預約資料，包含預約者、會議室、管理者資訊
            var reservation = db.Conference
                .Include(c => c.CreateByNavigation)
                .Include(c => c.ConferenceRoomSlots)
                    .ThenInclude(s => s.Room)
                        .ThenInclude(r => r.Manager)
                .FirstOrDefault(c => c.Id == conferenceId);

            if (reservation == null)
            {
                Console.WriteLine($"📧 [ReservationCreated] 找不到預約資料，ConferenceId: {conferenceId}");
                return;
            }

            Console.WriteLine($"📧 [ReservationCreated] 找到預約: {reservation.Name}");

            // 取得預約者 Email
            var applicantEmail = reservation.CreateByNavigation?.Email;
            var applicantName = reservation.CreateByNavigation?.Name ?? "使用者";

            // 取得會議室資訊
            var roomSlot = reservation.ConferenceRoomSlots.FirstOrDefault();
            var room = roomSlot?.Room;
            var roomName = room != null ? $"{room.Building} {room.Floor} {room.Name}" : "未指定";
            var slotDate = roomSlot?.SlotDate.ToString("yyyy/MM/dd") ?? "-";
            var slotTime = reservation.ConferenceRoomSlots.Any()
                ? $"{reservation.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ {reservation.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm}"
                : "-";
            var bookingNo = reservation.Id.ToString().Substring(0, 8);

            // 1️⃣ 寄給預約者
            Console.WriteLine($"📧 [ReservationCreated] 預約者 Email: {applicantEmail ?? "NULL"}");

            if (!string.IsNullOrEmpty(applicantEmail))
            {
                var userMail = NewMailMessage();
                userMail.Subject = $"[預約申請已送出] - {reservation.Name}";
                userMail.Body = BuildReservationCreatedBody(
                    applicantName,
                    bookingNo,
                    reservation.Name,
                    reservation.OrganizerUnit,
                    reservation.Chairman,
                    roomName,
                    slotDate,
                    slotTime,
                    reservation.TotalAmount
                );
                userMail.To.Add(applicantEmail);

                Console.WriteLine($"📧 [ReservationCreated] 準備寄信給預約者: {applicantEmail}");
                try
                {
                    SendAsync(userMail, className, functionName).GetAwaiter().GetResult();
                    Console.WriteLine($"📧 [ReservationCreated] ✅ 預約者信件已發送: {applicantEmail}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"📧 [ReservationCreated] ❌ 預約者信件發送失敗: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("📧 [ReservationCreated] ⚠️ 預約者沒有 Email，跳過");
            }

            // 2️⃣ 寄給會議室管理者
            var managerEmail = room?.Manager?.Email;
            var managerName = room?.Manager?.Name ?? "管理者";

            Console.WriteLine($"📧 [ReservationCreated] 管理者 Email: {managerEmail ?? "NULL"}");

            if (!string.IsNullOrEmpty(managerEmail))
            {
                var managerMail = NewMailMessage();
                managerMail.Subject = $"[新預約待審核] - {reservation.Name}";
                managerMail.Body = BuildReservationPendingApprovalBody(
                    managerName,
                    applicantName,
                    bookingNo,
                    reservation.Name,
                    reservation.OrganizerUnit,
                    reservation.Chairman,
                    roomName,
                    slotDate,
                    slotTime,
                    reservation.TotalAmount
                );
                managerMail.To.Add(managerEmail);

                Console.WriteLine($"📧 [ReservationCreated] 準備寄信給管理者: {managerEmail}");
                try
                {
                    SendAsync(managerMail, className, functionName).GetAwaiter().GetResult();
                    Console.WriteLine($"📧 [ReservationCreated] ✅ 管理者信件已發送: {managerEmail}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"📧 [ReservationCreated] ❌ 管理者信件發送失敗: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("📧 [ReservationCreated] ⚠️ 會議室沒有管理者或管理者沒有 Email，跳過");
            }
        }

        /// <summary>
        /// 建立預約者收到的郵件內容
        /// </summary>
        private string BuildReservationCreatedBody(
            string applicantName,
            string bookingNo,
            string conferenceName,
            string organizerUnit,
            string chairman,
            string roomName,
            string slotDate,
            string slotTime,
            int totalAmount)
        {
            return $@"
<h3>親愛的 {applicantName} 您好：</h3>

<p>您的會議室預約申請已成功送出，目前正在等待審核。</p>

<h4>預約資訊</h4>
<table style='border-collapse: collapse; width: 100%; max-width: 500px;'>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5; width: 120px;'><strong>預約單號</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{bookingNo}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議名稱</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{conferenceName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>承辦單位</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{organizerUnit ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議主席</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{chairman ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議室</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{roomName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約日期</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotDate}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約時間</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotTime}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預估金額</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>NT$ {totalAmount:N0}</td>
    </tr>
</table>

<p style='margin-top: 20px;'>
    <strong>目前狀態：待審核</strong><br/>
    審核結果將以 Email 通知您，請耐心等候。
</p>

<p style='margin-top: 20px;'>
    您可以至 <a href='{BaseUrl}ReservationOverview'>預約總覽</a> 查看預約狀態。
</p>

<p style='color: #666; margin-top: 30px;'>
    如有任何問題，請聯繫場地管理單位。
</p>
";
        }

        /// <summary>
        /// 建立管理者收到的郵件內容
        /// </summary>
        private string BuildReservationPendingApprovalBody(
            string managerName,
            string applicantName,
            string bookingNo,
            string conferenceName,
            string organizerUnit,
            string chairman,
            string roomName,
            string slotDate,
            string slotTime,
            int totalAmount)
        {
            return $@"
<h3>親愛的 {managerName} 您好：</h3>

<p>有一筆新的會議室預約申請需要您審核。</p>

<h4>📋 預約資訊</h4>
<table style='border-collapse: collapse; width: 100%; max-width: 500px;'>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5; width: 120px;'><strong>預約單號</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{bookingNo}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約者</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{applicantName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議名稱</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{conferenceName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>承辦單位</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{organizerUnit ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議主席</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{chairman ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議室</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{roomName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約日期</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotDate}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約時間</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotTime}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預估金額</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>NT$ {totalAmount:N0}</td>
    </tr>
</table>

<p style='margin-top: 20px;'>
    <strong>🔔 請前往系統進行審核</strong>
</p>

<p>
    <a href='{BaseUrl}Admin/Reservation' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>
        前往審核
    </a>
</p>

<p style='color: #666; margin-top: 30px;'>
    請盡快處理，謝謝！
</p>
";
        }

        /// <summary>
        /// 審核通過通知 - 通知使用者去繳費
        /// </summary>
        public void ReservationApproved(Guid conferenceId, int? discountAmount, string? discountReason, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            Console.WriteLine($"📧 [ReservationApproved] 開始處理，ConferenceId: {conferenceId}");

            if (!Enable)
            {
                Console.WriteLine("📧 [ReservationApproved] 信件服務未啟用，跳過");
                return;
            }

            using var db = dbContextFactory.CreateDbContext();

            var reservation = db.Conference
                .Include(c => c.CreateByNavigation)
                .Include(c => c.ConferenceRoomSlots)
                    .ThenInclude(s => s.Room)
                .FirstOrDefault(c => c.Id == conferenceId);

            if (reservation == null)
            {
                Console.WriteLine($"📧 [ReservationApproved] 找不到預約資料，ConferenceId: {conferenceId}");
                return;
            }

            var applicantEmail = reservation.CreateByNavigation?.Email;
            var applicantName = reservation.CreateByNavigation?.Name ?? "使用者";

            if (string.IsNullOrEmpty(applicantEmail))
            {
                Console.WriteLine("📧 [ReservationApproved] ⚠️ 預約者沒有 Email，跳過");
                return;
            }

            var roomSlot = reservation.ConferenceRoomSlots.FirstOrDefault();
            var room = roomSlot?.Room;
            var roomName = room != null ? $"{room.Building} {room.Floor} {room.Name}" : "未指定";
            var slotDate = roomSlot?.SlotDate.ToString("yyyy/MM/dd") ?? "-";
            var slotTime = reservation.ConferenceRoomSlots.Any()
                ? $"{reservation.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ {reservation.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm}"
                : "-";
            var bookingNo = reservation.Id.ToString().Substring(0, 8);
            var paymentDeadline = reservation.PaymentDeadline?.ToString("yyyy/MM/dd") ?? "-";

            var mail = NewMailMessage();
            mail.Subject = $"[預約審核通過] - {reservation.Name}";
            mail.Body = BuildReservationApprovedBody(
                applicantName,
                bookingNo,
                reservation.Name,
                reservation.OrganizerUnit,
                reservation.Chairman,
                roomName,
                slotDate,
                slotTime,
                reservation.TotalAmount,
                paymentDeadline,
                reservation.PaymentMethod,
                discountAmount,
                discountReason
            );
            mail.To.Add(applicantEmail);

            Console.WriteLine($"📧 [ReservationApproved] 準備寄信給預約者: {applicantEmail}");
            try
            {
                SendAsync(mail, className, functionName).GetAwaiter().GetResult();
                Console.WriteLine($"📧 [ReservationApproved] ✅ 信件已發送: {applicantEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📧 [ReservationApproved] ❌ 信件發送失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 審核拒絕通知
        /// </summary>
        public void ReservationRejected(Guid conferenceId, string? rejectReason, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            Console.WriteLine($"📧 [ReservationRejected] 開始處理，ConferenceId: {conferenceId}");

            if (!Enable)
            {
                Console.WriteLine("📧 [ReservationRejected] 信件服務未啟用，跳過");
                return;
            }

            using var db = dbContextFactory.CreateDbContext();

            var reservation = db.Conference
                .Include(c => c.CreateByNavigation)
                .Include(c => c.ConferenceRoomSlots)
                    .ThenInclude(s => s.Room)
                .FirstOrDefault(c => c.Id == conferenceId);

            if (reservation == null)
            {
                Console.WriteLine($"📧 [ReservationRejected] 找不到預約資料，ConferenceId: {conferenceId}");
                return;
            }

            var applicantEmail = reservation.CreateByNavigation?.Email;
            var applicantName = reservation.CreateByNavigation?.Name ?? "使用者";

            if (string.IsNullOrEmpty(applicantEmail))
            {
                Console.WriteLine("📧 [ReservationRejected] ⚠️ 預約者沒有 Email，跳過");
                return;
            }

            var roomSlot = reservation.ConferenceRoomSlots.FirstOrDefault();
            var room = roomSlot?.Room;
            var roomName = room != null ? $"{room.Building} {room.Floor} {room.Name}" : "未指定";
            var slotDate = roomSlot?.SlotDate.ToString("yyyy/MM/dd") ?? "-";
            var slotTime = reservation.ConferenceRoomSlots.Any()
                ? $"{reservation.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ {reservation.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm}"
                : "-";
            var bookingNo = reservation.Id.ToString().Substring(0, 8);

            var mail = NewMailMessage();
            mail.Subject = $"[預約審核未通過] - {reservation.Name}";
            mail.Body = BuildReservationRejectedBody(
                applicantName,
                bookingNo,
                reservation.Name,
                reservation.OrganizerUnit,
                reservation.Chairman,
                roomName,
                slotDate,
                slotTime,
                rejectReason
            );
            mail.To.Add(applicantEmail);

            Console.WriteLine($"📧 [ReservationRejected] 準備寄信給預約者: {applicantEmail}");
            try
            {
                SendAsync(mail, className, functionName).GetAwaiter().GetResult();
                Console.WriteLine($"📧 [ReservationRejected] ✅ 信件已發送: {applicantEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📧 [ReservationRejected] ❌ 信件發送失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 建立審核通過郵件內容
        /// </summary>
        private string BuildReservationApprovedBody(
            string applicantName,
            string bookingNo,
            string conferenceName,
            string organizerUnit,
            string chairman,
            string roomName,
            string slotDate,
            string slotTime,
            int totalAmount,
            string paymentDeadline,
            string paymentMethod,
            int? discountAmount,
            string? discountReason)
        {
            // 付款方式說明
            var paymentInfo = paymentMethod switch
            {
                "transfer" => @"
<div style='background-color: #e7f3ff; border-left: 4px solid #007bff; padding: 15px; margin: 15px 0;'>
    <h4 style='margin: 0 0 10px 0; color: #007bff;'>💳 銀行匯款資訊</h4>
    <p style='margin: 5px 0;'><strong>銀行名稱：</strong>合作金庫(006)</p>
    <p style='margin: 5px 0;'><strong>分行名稱：</strong>石牌分行</p>
    <p style='margin: 5px 0;'><strong>戶名：</strong>臺北榮民總醫院作業基金403專戶</p>
    <p style='margin: 5px 0;'><strong>帳號：</strong>1427713000733</p>
    <p style='margin: 10px 0 0 0; color: #666;'><small>匯款完成後，請至系統上傳轉帳末五碼及金額</small></p>
</div>",
                "cash" => @"
<div style='background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 15px 0;'>
    <h4 style='margin: 0 0 10px 0; color: #856404;'>🏢 現金繳費資訊</h4>
    <p style='margin: 5px 0;'><strong>繳費地點：</strong>中正樓與北護分院連通道的郵局旁</p>
    <p style='margin: 5px 0;'><strong>服務時間：</strong>週一至週五 8:00AM - 5:00PM/5:30PM</p>
    <p style='margin: 10px 0 0 0; color: #666;'><small>繳費完成後，請至系統上傳繳費收據</small></p>
</div>",
                "cost-sharing" => @"
<div style='background-color: #d4edda; border-left: 4px solid #28a745; padding: 15px; margin: 15px 0;'>
    <h4 style='margin: 0 0 10px 0; color: #155724;'>📊 成本分攤</h4>
    <p style='margin: 5px 0;'>此預約採用成本分攤方式，費用將由指定的成本中心支付。</p>
    <p style='margin: 5px 0;'>無需另行繳費，系統將自動處理。</p>
</div>",
                _ => ""
            };

            // 折扣說明
            var discountInfo = "";
            if (discountAmount.HasValue && discountAmount.Value > 0)
            {
                discountInfo = $@"
<div style='background-color: #d4edda; border-left: 4px solid #28a745; padding: 15px; margin: 15px 0;'>
    <h4 style='margin: 0 0 10px 0; color: #155724;'>優惠折扣</h4>
    <p style='margin: 5px 0;'><strong>折扣金額：</strong>NT$ {discountAmount.Value:N0}</p>
    {(string.IsNullOrEmpty(discountReason) ? "" : $"<p style='margin: 5px 0;'><strong>折扣原因：</strong>{discountReason}</p>")}
</div>";
            }

            return $@"
<h3>親愛的 {applicantName} 您好：</h3>

<p><strong>您的會議室預約申請已審核通過。</strong></p>

<h4>預約資訊</h4>
<table style='border-collapse: collapse; width: 100%; max-width: 500px;'>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5; width: 120px;'><strong>預約單號</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{bookingNo}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議名稱</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{conferenceName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>承辦單位</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{organizerUnit ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議主席</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{chairman ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議室</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{roomName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約日期</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotDate}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約時間</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotTime}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>應繳金額</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'><strong style='color: #dc3545;'>NT$ {totalAmount:N0}</strong></td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>繳費期限</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'><strong style='color: #dc3545;'>{paymentDeadline}</strong></td>
    </tr>
</table>

{discountInfo}

{paymentInfo}

<p style='margin-top: 20px;'>
    <a href='{BaseUrl}ReservationOverview' style='display: inline-block; padding: 10px 20px; background-color: #28a745; color: white; text-decoration: none; border-radius: 5px;'>
        前往繳費
    </a>
</p>

<p style='color: #dc3545; margin-top: 20px;'>
    <strong>⚠️ 請注意：</strong>逾期未繳費，預約將自動取消，時段將釋放給其他人預約。
</p>

<p style='color: #666; margin-top: 30px;'>
    如有任何問題，請聯繫場地管理單位。
</p>
";
        }

        /// <summary>
        /// 建立審核拒絕郵件內容
        /// </summary>
        private string BuildReservationRejectedBody(
            string applicantName,
            string bookingNo,
            string conferenceName,
            string organizerUnit,
            string chairman,
            string roomName,
            string slotDate,
            string slotTime,
            string? rejectReason)
        {
            return $@"
<h3>親愛的 {applicantName} 您好：</h3>

<p>很抱歉，您的會議室預約申請未通過審核。</p>

<h4>預約資訊</h4>
<table style='border-collapse: collapse; width: 100%; max-width: 500px;'>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5; width: 120px;'><strong>預約單號</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{bookingNo}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議名稱</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{conferenceName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>承辦單位</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{organizerUnit ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議主席</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{chairman ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議室</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{roomName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約日期</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotDate}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約時間</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotTime}</td>
    </tr>
</table>

<div style='background-color: #f8d7da; border-left: 4px solid #dc3545; padding: 15px; margin: 15px 0;'>
    <h4 style='margin: 0 0 10px 0; color: #721c24;'>❌ 拒絕原因</h4>
    <p style='margin: 0; color: #721c24;'>{(string.IsNullOrEmpty(rejectReason) ? "未提供原因" : rejectReason)}</p>
</div>

<p style='margin-top: 20px;'>
    如有疑問，請聯繫場地管理單位。您也可以重新提交預約申請。
</p>

<p>
    <a href='{BaseUrl}Conference/Create' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>
        重新預約
    </a>
</p>

<p style='color: #666; margin-top: 30px;'>
    感謝您的理解與配合。
</p>
";
        }

        /// <summary>
        /// 繳費資訊上傳通知 - 通知該分院的總務
        /// </summary>
        public void PaymentProofUploaded(Guid conferenceId, string paymentType, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            Console.WriteLine($"📧 [PaymentProofUploaded] 開始處理，ConferenceId: {conferenceId}");

            if (!Enable)
            {
                Console.WriteLine("📧 [PaymentProofUploaded] 信件服務未啟用，跳過");
                return;
            }

            using var db = dbContextFactory.CreateDbContext();

            // 取得預約資料
            var reservation = db.Conference
                .Include(c => c.CreateByNavigation)
                .Include(c => c.ConferenceRoomSlots)
                    .ThenInclude(s => s.Room)
                .FirstOrDefault(c => c.Id == conferenceId);

            if (reservation == null)
            {
                Console.WriteLine($"📧 [PaymentProofUploaded] 找不到預約資料，ConferenceId: {conferenceId}");
                return;
            }

            var applicantName = reservation.CreateByNavigation?.Name ?? "使用者";
            var departmentId = reservation.DepartmentId;

            // 取得會議室資訊
            var roomSlot = reservation.ConferenceRoomSlots.FirstOrDefault();
            var room = roomSlot?.Room;
            var roomName = room != null ? $"{room.Building} {room.Floor} {room.Name}" : "未指定";
            var slotDate = roomSlot?.SlotDate.ToString("yyyy/MM/dd") ?? "-";
            var slotTime = reservation.ConferenceRoomSlots.Any()
                ? $"{reservation.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ {reservation.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm}"
                : "-";
            var bookingNo = reservation.Id.ToString().Substring(0, 8);

            // 找到該分院的總務人員 (ACCOUNTANT 角色)
            var accountants = db.AuthUser
                .Include(u => u.AuthRole)
                .Where(u => u.DeleteAt == null
                         && u.IsEnabled
                         && u.DepartmentId == departmentId
                         && u.AuthRole.Any(r => r.Code == "ACCOUNTANT"))
                .ToList();

            Console.WriteLine($"📧 [PaymentProofUploaded] 分院ID: {departmentId}, 找到 {accountants.Count} 位總務人員");

            if (!accountants.Any())
            {
                // 如果找不到該分院的總務，嘗試找全系統的總務
                accountants = db.AuthUser
                    .Include(u => u.AuthRole)
                    .Where(u => u.DeleteAt == null
                             && u.IsEnabled
                             && u.AuthRole.Any(r => r.Code == "ACCOUNTANT"))
                    .ToList();

                Console.WriteLine($"📧 [PaymentProofUploaded] 找不到該分院總務，改用全系統總務: {accountants.Count} 位");
            }

            foreach (var accountant in accountants)
            {
                if (string.IsNullOrEmpty(accountant.Email))
                {
                    Console.WriteLine($"📧 [PaymentProofUploaded] ⚠️ 總務 {accountant.Name} 沒有 Email，跳過");
                    continue;
                }

                var mail = NewMailMessage();
                mail.Subject = $"[繳費憑證待審核] - {reservation.Name}";
                mail.Body = BuildPaymentProofUploadedBody(
                    accountant.Name,
                    applicantName,
                    bookingNo,
                    reservation.Name,
                    reservation.OrganizerUnit,
                    reservation.Chairman,
                    roomName,
                    slotDate,
                    slotTime,
                    reservation.TotalAmount,
                    paymentType
                );
                mail.To.Add(accountant.Email);

                Console.WriteLine($"📧 [PaymentProofUploaded] 準備寄信給總務: {accountant.Email}");
                try
                {
                    SendAsync(mail, className, functionName).GetAwaiter().GetResult();
                    Console.WriteLine($"📧 [PaymentProofUploaded] ✅ 信件已發送: {accountant.Email}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"📧 [PaymentProofUploaded] ❌ 信件發送失敗: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 建立繳費憑證上傳通知郵件內容
        /// </summary>
        private string BuildPaymentProofUploadedBody(
            string accountantName,
            string applicantName,
            string bookingNo,
            string conferenceName,
            string organizerUnit,
            string chairman,
            string roomName,
            string slotDate,
            string slotTime,
            int totalAmount,
            string paymentType)
        {
            return $@"
<h3>親愛的 {accountantName} 您好：</h3>

<p>有一筆新的繳費憑證需要您審核。</p>

<h4>📋 預約資訊</h4>
<table style='border-collapse: collapse; width: 100%; max-width: 500px;'>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5; width: 120px;'><strong>預約單號</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{bookingNo}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約者</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{applicantName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議名稱</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{conferenceName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>承辦單位</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{organizerUnit ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議主席</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{chairman ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議室</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{roomName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約日期</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotDate}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約時間</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotTime}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>應繳金額</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>NT$ {totalAmount:N0}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>付款方式</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{paymentType}</td>
    </tr>
</table>

<p style='margin-top: 20px;'>
    <strong>🔔 請前往系統審核繳費憑證</strong>
</p>

<p>
    <a href='{BaseUrl}Admin/Reservation?tab=payment' style='display: inline-block; padding: 10px 20px; background-color: #28a745; color: white; text-decoration: none; border-radius: 5px;'>
        前往審核
    </a>
</p>

<p style='color: #666; margin-top: 30px;'>
    請盡快處理，謝謝！
</p>
";
        }

        /// <summary>
        /// 繳費審核通過通知 - 通知使用者預約成功
        /// </summary>
        public void PaymentApproved(Guid conferenceId, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            Console.WriteLine($"📧 [PaymentApproved] 開始處理，ConferenceId: {conferenceId}");

            if (!Enable)
            {
                Console.WriteLine("📧 [PaymentApproved] 信件服務未啟用，跳過");
                return;
            }

            using var db = dbContextFactory.CreateDbContext();

            var reservation = db.Conference
                .Include(c => c.CreateByNavigation)
                .Include(c => c.ConferenceRoomSlots)
                    .ThenInclude(s => s.Room)
                .FirstOrDefault(c => c.Id == conferenceId);

            if (reservation == null)
            {
                Console.WriteLine($"📧 [PaymentApproved] 找不到預約資料，ConferenceId: {conferenceId}");
                return;
            }

            var applicantEmail = reservation.CreateByNavigation?.Email;
            var applicantName = reservation.CreateByNavigation?.Name ?? "使用者";

            if (string.IsNullOrEmpty(applicantEmail))
            {
                Console.WriteLine("📧 [PaymentApproved] ⚠️ 預約者沒有 Email，跳過");
                return;
            }

            var roomSlot = reservation.ConferenceRoomSlots.FirstOrDefault();
            var room = roomSlot?.Room;
            var roomName = room != null ? $"{room.Building} {room.Floor} {room.Name}" : "未指定";
            var slotDate = roomSlot?.SlotDate.ToString("yyyy/MM/dd") ?? "-";
            var slotTime = reservation.ConferenceRoomSlots.Any()
                ? $"{reservation.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ {reservation.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm}"
                : "-";
            var bookingNo = reservation.Id.ToString().Substring(0, 8);

            var mail = NewMailMessage();
            mail.Subject = $"[預約成功] - {reservation.Name}";
            mail.Body = BuildPaymentApprovedBody(
                applicantName,
                bookingNo,
                reservation.Name,
                reservation.OrganizerUnit,
                reservation.Chairman,
                roomName,
                slotDate,
                slotTime,
                reservation.TotalAmount
            );
            mail.To.Add(applicantEmail);

            Console.WriteLine($"📧 [PaymentApproved] 準備寄信給預約者: {applicantEmail}");
            try
            {
                SendAsync(mail, className, functionName).GetAwaiter().GetResult();
                Console.WriteLine($"📧 [PaymentApproved] ✅ 信件已發送: {applicantEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📧 [PaymentApproved] ❌ 信件發送失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 繳費審核拒絕通知 - 通知使用者重新上傳
        /// </summary>
        public void PaymentRejected(Guid conferenceId, string? rejectReason, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            Console.WriteLine($"📧 [PaymentRejected] 開始處理，ConferenceId: {conferenceId}");

            if (!Enable)
            {
                Console.WriteLine("📧 [PaymentRejected] 信件服務未啟用，跳過");
                return;
            }

            using var db = dbContextFactory.CreateDbContext();

            var reservation = db.Conference
                .Include(c => c.CreateByNavigation)
                .Include(c => c.ConferenceRoomSlots)
                    .ThenInclude(s => s.Room)
                .FirstOrDefault(c => c.Id == conferenceId);

            if (reservation == null)
            {
                Console.WriteLine($"📧 [PaymentRejected] 找不到預約資料，ConferenceId: {conferenceId}");
                return;
            }

            var applicantEmail = reservation.CreateByNavigation?.Email;
            var applicantName = reservation.CreateByNavigation?.Name ?? "使用者";

            if (string.IsNullOrEmpty(applicantEmail))
            {
                Console.WriteLine("📧 [PaymentRejected] ⚠️ 預約者沒有 Email，跳過");
                return;
            }

            var roomSlot = reservation.ConferenceRoomSlots.FirstOrDefault();
            var room = roomSlot?.Room;
            var roomName = room != null ? $"{room.Building} {room.Floor} {room.Name}" : "未指定";
            var slotDate = roomSlot?.SlotDate.ToString("yyyy/MM/dd") ?? "-";
            var slotTime = reservation.ConferenceRoomSlots.Any()
                ? $"{reservation.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ {reservation.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm}"
                : "-";
            var bookingNo = reservation.Id.ToString().Substring(0, 8);
            var paymentDeadline = reservation.PaymentDeadline?.ToString("yyyy/MM/dd") ?? "-";

            var mail = NewMailMessage();
            mail.Subject = $"[繳費憑證需重新上傳] - {reservation.Name}";
            mail.Body = BuildPaymentRejectedBody(
                applicantName,
                bookingNo,
                reservation.Name,
                reservation.OrganizerUnit,
                reservation.Chairman,
                roomName,
                slotDate,
                slotTime,
                reservation.TotalAmount,
                paymentDeadline,
                rejectReason
            );
            mail.To.Add(applicantEmail);

            Console.WriteLine($"📧 [PaymentRejected] 準備寄信給預約者: {applicantEmail}");
            try
            {
                SendAsync(mail, className, functionName).GetAwaiter().GetResult();
                Console.WriteLine($"📧 [PaymentRejected] ✅ 信件已發送: {applicantEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📧 [PaymentRejected] ❌ 信件發送失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 建立繳費審核通過郵件內容
        /// </summary>
        private string BuildPaymentApprovedBody(
            string applicantName,
            string bookingNo,
            string conferenceName,
            string organizerUnit,
            string chairman,
            string roomName,
            string slotDate,
            string slotTime,
            int totalAmount)
        {
            return $@"
<h3>親愛的 {applicantName} 您好：</h3>

<p>🎉 <strong>恭喜！您的繳費已確認，預約正式成立！</strong></p>

<h4>📋 預約資訊</h4>
<table style='border-collapse: collapse; width: 100%; max-width: 500px;'>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5; width: 120px;'><strong>預約單號</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{bookingNo}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議名稱</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{conferenceName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>承辦單位</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{organizerUnit ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議主席</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{chairman ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議室</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{roomName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約日期</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotDate}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約時間</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotTime}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>已繳金額</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>NT$ {totalAmount:N0}</td>
    </tr>
</table>

<div style='background-color: #d4edda; border-left: 4px solid #28a745; padding: 15px; margin: 15px 0;'>
    <h4 style='margin: 0 0 10px 0; color: #155724;'>✅ 預約已確認</h4>
    <p style='margin: 0; color: #155724;'>請於預約時間準時使用會議室。如需取消預約，請提前至系統操作。</p>
</div>

<p style='margin-top: 20px;'>
    <a href='{BaseUrl}ReservationOverview' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>
        查看預約詳情
    </a>
</p>

<p style='color: #666; margin-top: 30px;'>
    感謝您的使用，祝您會議順利！
</p>
";
        }

        /// <summary>
        /// 建立繳費審核拒絕郵件內容
        /// </summary>
        private string BuildPaymentRejectedBody(
            string applicantName,
            string bookingNo,
            string conferenceName,
            string organizerUnit,
            string chairman,
            string roomName,
            string slotDate,
            string slotTime,
            int totalAmount,
            string paymentDeadline,
            string? rejectReason)
        {
            return $@"
<h3>親愛的 {applicantName} 您好：</h3>

<p>您的繳費憑證審核未通過，請重新上傳正確的憑證。</p>

<h4>📋 預約資訊</h4>
<table style='border-collapse: collapse; width: 100%; max-width: 500px;'>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5; width: 120px;'><strong>預約單號</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{bookingNo}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議名稱</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{conferenceName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>承辦單位</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{organizerUnit ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議主席</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{chairman ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議室</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{roomName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約日期</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotDate}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約時間</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotTime}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>應繳金額</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>NT$ {totalAmount:N0}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>繳費期限</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'><strong style='color: #dc3545;'>{paymentDeadline}</strong></td>
    </tr>
</table>

<div style='background-color: #f8d7da; border-left: 4px solid #dc3545; padding: 15px; margin: 15px 0;'>
    <h4 style='margin: 0 0 10px 0; color: #721c24;'>❌ 退回原因</h4>
    <p style='margin: 0; color: #721c24;'>{(string.IsNullOrEmpty(rejectReason) ? "未提供原因" : rejectReason)}</p>
</div>

<p style='margin-top: 20px;'>
    <strong>⚠️ 請於繳費期限內重新上傳正確的憑證，逾期預約將自動取消。</strong>
</p>

<p>
    <a href='{BaseUrl}ReservationOverview' style='display: inline-block; padding: 10px 20px; background-color: #ffc107; color: #212529; text-decoration: none; border-radius: 5px;'>
        重新上傳憑證
    </a>
</p>

<p style='color: #666; margin-top: 30px;'>
    如有任何問題，請聯繫場地管理單位。
</p>
";
        }

        /// <summary>
        /// 取消預約退費通知 - 通知總務退費
        /// </summary>
        public void RefundNotify(Guid conferenceId, int daysUntilReservation, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            Console.WriteLine($"📧 [RefundNotify] 開始處理，ConferenceId: {conferenceId}");

            if (!Enable)
            {
                Console.WriteLine("📧 [RefundNotify] 信件服務未啟用，跳過");
                return;
            }

            using var db = dbContextFactory.CreateDbContext();

            var reservation = db.Conference
                .Include(c => c.CreateByNavigation)
                .Include(c => c.ConferenceRoomSlots)
                    .ThenInclude(s => s.Room)
                .FirstOrDefault(c => c.Id == conferenceId);

            if (reservation == null)
            {
                Console.WriteLine($"📧 [RefundNotify] 找不到預約資料，ConferenceId: {conferenceId}");
                return;
            }

            var applicantName = reservation.CreateByNavigation?.Name ?? "使用者";
            var applicantEmail = reservation.CreateByNavigation?.Email ?? "-";
            var departmentId = reservation.DepartmentId;

            var roomSlot = reservation.ConferenceRoomSlots.FirstOrDefault();
            var room = roomSlot?.Room;
            var roomName = room != null ? $"{room.Building} {room.Floor} {room.Name}" : "未指定";
            var slotDate = roomSlot?.SlotDate.ToString("yyyy/MM/dd") ?? "-";
            var slotTime = reservation.ConferenceRoomSlots.Any()
                ? $"{reservation.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ {reservation.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm}"
                : "-";
            var bookingNo = reservation.Id.ToString().Substring(0, 8);
            var paymentMethod = reservation.PaymentMethod switch
            {
                "transfer" => "銀行匯款",
                "cash" => "現金繳費",
                "cost-sharing" => "成本分攤",
                _ => reservation.PaymentMethod ?? "-"
            };

            // 找到該分院的總務人員 (ACCOUNTANT 角色)
            var accountants = db.AuthUser
                .Include(u => u.AuthRole)
                .Where(u => u.DeleteAt == null
                         && u.IsEnabled
                         && u.DepartmentId == departmentId
                         && u.AuthRole.Any(r => r.Code == "ACCOUNTANT"))
                .ToList();

            if (!accountants.Any())
            {
                accountants = db.AuthUser
                    .Include(u => u.AuthRole)
                    .Where(u => u.DeleteAt == null
                             && u.IsEnabled
                             && u.AuthRole.Any(r => r.Code == "ACCOUNTANT"))
                    .ToList();
            }

            Console.WriteLine($"📧 [RefundNotify] 找到 {accountants.Count} 位總務人員");

            foreach (var accountant in accountants)
            {
                if (string.IsNullOrEmpty(accountant.Email))
                {
                    Console.WriteLine($"📧 [RefundNotify] ⚠️ 總務 {accountant.Name} 沒有 Email，跳過");
                    continue;
                }

                var mail = NewMailMessage();
                mail.Subject = $"[預約取消退費通知] - {reservation.Name}";
                mail.Body = BuildRefundNotifyBody(
                    accountant.Name,
                    applicantName,
                    applicantEmail,
                    bookingNo,
                    reservation.Name,
                    reservation.OrganizerUnit,
                    reservation.Chairman,
                    roomName,
                    slotDate,
                    slotTime,
                    reservation.TotalAmount,
                    paymentMethod,
                    daysUntilReservation
                );
                mail.To.Add(accountant.Email);

                Console.WriteLine($"📧 [RefundNotify] 準備寄信給總務: {accountant.Email}");
                try
                {
                    SendAsync(mail, className, functionName).GetAwaiter().GetResult();
                    Console.WriteLine($"📧 [RefundNotify] ✅ 信件已發送: {accountant.Email}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"📧 [RefundNotify] ❌ 信件發送失敗: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 建立退費通知郵件內容
        /// </summary>
        private string BuildRefundNotifyBody(
            string accountantName,
            string applicantName,
            string applicantEmail,
            string bookingNo,
            string conferenceName,
            string organizerUnit,
            string chairman,
            string roomName,
            string slotDate,
            string slotTime,
            int totalAmount,
            string paymentMethod,
            int daysUntilReservation)
        {
            return $@"
<h3>親愛的 {accountantName} 您好：</h3>

<p>有一筆<strong style='color: #dc3545;'>已繳費的預約被取消</strong>，請協助辦理退費。</p>

<h4>📋 預約資訊</h4>
<table style='border-collapse: collapse; width: 100%; max-width: 500px;'>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5; width: 120px;'><strong>預約單號</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{bookingNo}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約者</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{applicantName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約者信箱</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{applicantEmail}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議名稱</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{conferenceName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>承辦單位</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{organizerUnit ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議主席</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{chairman ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議室</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{roomName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>原預約日期</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotDate}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>原預約時間</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotTime}</td>
    </tr>
</table>

<div style='background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 15px 0;'>
    <h4 style='margin: 0 0 10px 0; color: #856404;'>💰 退費資訊</h4>
    <p style='margin: 5px 0;'><strong>已繳金額：</strong><span style='color: #dc3545; font-size: 1.2em;'>NT$ {totalAmount:N0}</span></p>
    <p style='margin: 5px 0;'><strong>付款方式：</strong>{paymentMethod}</p>
    <p style='margin: 5px 0;'><strong>取消時距離會議：</strong>{daysUntilReservation} 天</p>
</div>

<p style='color: #666;'>
    請依據退費規定計算應退金額，並聯繫預約者辦理退費事宜。
</p>

<p style='color: #666; margin-top: 30px;'>
    此為系統自動通知，請盡快處理，謝謝！
</p>
";
        }

        /// <summary>
        /// 繳費逾期自動取消通知 - 通知用戶預約已被系統自動取消
        /// </summary>
        public void PaymentOverdueCancelled(Guid conferenceId, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            Console.WriteLine($"📧 [PaymentOverdueCancelled] 開始處理，ConferenceId: {conferenceId}");

            if (!Enable)
            {
                Console.WriteLine("📧 [PaymentOverdueCancelled] 信件服務未啟用，跳過");
                return;
            }

            using var db = dbContextFactory.CreateDbContext();

            var reservation = db.Conference
            .IgnoreQueryFilters()
                .Include(c => c.CreateByNavigation)
                .Include(c => c.ConferenceRoomSlots)
                    .ThenInclude(s => s.Room)
                .FirstOrDefault(c => c.Id == conferenceId);

            if (reservation == null)
            {
                Console.WriteLine($"📧 [PaymentOverdueCancelled] 找不到預約資料，ConferenceId: {conferenceId}");
                return;
            }

            var applicantEmail = reservation.CreateByNavigation?.Email;
            var applicantName = reservation.CreateByNavigation?.Name ?? "使用者";

            if (string.IsNullOrEmpty(applicantEmail))
            {
                Console.WriteLine("📧 [PaymentOverdueCancelled] ⚠️ 預約者沒有 Email，跳過");
                return;
            }

            var roomSlot = reservation.ConferenceRoomSlots.FirstOrDefault();
            var room = roomSlot?.Room;
            var roomName = room != null ? $"{room.Building} {room.Floor} {room.Name}" : "未指定";
            var slotDate = roomSlot?.SlotDate.ToString("yyyy/MM/dd") ?? "-";
            var slotTime = reservation.ConferenceRoomSlots.Any()
                ? $"{reservation.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ {reservation.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm}"
                : "-";
            var bookingNo = reservation.Id.ToString().Substring(0, 8);
            var paymentDeadline = reservation.PaymentDeadline?.ToString("yyyy/MM/dd") ?? "-";

            var mail = NewMailMessage();
            mail.Subject = $"[預約已取消] 繳費逾期 - {reservation.Name}";
            mail.Body = BuildPaymentOverdueCancelledBody(
                applicantName,
                bookingNo,
                reservation.Name,
                reservation.OrganizerUnit,
                reservation.Chairman,
                roomName,
                slotDate,
                slotTime,
                reservation.TotalAmount,
                paymentDeadline
            );
            mail.To.Add(applicantEmail);

            Console.WriteLine($"📧 [PaymentOverdueCancelled] 準備寄信給預約者: {applicantEmail}");
            try
            {
                SendAsync(mail, className, functionName).GetAwaiter().GetResult();
                Console.WriteLine($"📧 [PaymentOverdueCancelled] ✅ 信件已發送: {applicantEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📧 [PaymentOverdueCancelled] ❌ 信件發送失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 建立繳費逾期取消郵件內容
        /// </summary>
        private string BuildPaymentOverdueCancelledBody(
            string applicantName,
            string bookingNo,
            string conferenceName,
            string organizerUnit,
            string chairman,
            string roomName,
            string slotDate,
            string slotTime,
            int totalAmount,
            string paymentDeadline)
        {
            return $@"
<h3>親愛的 {applicantName} 您好：</h3>

<div style='background-color: #f8d7da; border-left: 4px solid #dc3545; padding: 15px; margin: 15px 0;'>
    <h4 style='margin: 0; color: #721c24;'>❌ 您的預約已被系統自動取消</h4>
    <p style='margin: 10px 0 0 0; color: #721c24;'>因繳費期限（{paymentDeadline}）已過，系統已自動取消此預約，時段已釋放給其他人預約。</p>
</div>

<h4>📋 已取消的預約資訊</h4>
<table style='border-collapse: collapse; width: 100%; max-width: 500px;'>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5; width: 120px;'><strong>預約單號</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{bookingNo}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議名稱</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{conferenceName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>承辦單位</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{organizerUnit ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議主席</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{chairman ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議室</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{roomName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>原預約日期</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotDate}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>原預約時間</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotTime}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>原應繳金額</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>NT$ {totalAmount:N0}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>繳費期限</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{paymentDeadline}（已過期）</td>
    </tr>
</table>

<p style='margin-top: 20px;'>
    如仍需使用會議室，請重新提交預約申請。
</p>

<p>
    <a href='{BaseUrl}Conference/Create' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>
        重新預約
    </a>
</p>

<p style='color: #666; margin-top: 30px;'>
    如有任何問題，請聯繫場地管理單位。
</p>
";
        }

        /// <summary>
        /// 繳費期限提醒 - 通知預約者繳費期限快到了
        /// </summary>
        public void PaymentDeadlineReminder(Guid conferenceId, int daysUntilDeadline, [CallerFilePath] string className = "", [CallerMemberName] string functionName = "")
        {
            Console.WriteLine($"📧 [PaymentDeadlineReminder] 開始處理，ConferenceId: {conferenceId}, 剩餘 {daysUntilDeadline} 天");

            if (!Enable)
            {
                Console.WriteLine("📧 [PaymentDeadlineReminder] 信件服務未啟用，跳過");
                return;
            }

            using var db = dbContextFactory.CreateDbContext();

            var reservation = db.Conference
                .Include(c => c.CreateByNavigation)
                .Include(c => c.ConferenceRoomSlots)
                    .ThenInclude(s => s.Room)
                .FirstOrDefault(c => c.Id == conferenceId);

            if (reservation == null)
            {
                Console.WriteLine($"📧 [PaymentDeadlineReminder] 找不到預約資料，ConferenceId: {conferenceId}");
                return;
            }

            var applicantEmail = reservation.CreateByNavigation?.Email;
            var applicantName = reservation.CreateByNavigation?.Name ?? "使用者";

            if (string.IsNullOrEmpty(applicantEmail))
            {
                Console.WriteLine("📧 [PaymentDeadlineReminder] ⚠️ 預約者沒有 Email，跳過");
                return;
            }

            var roomSlot = reservation.ConferenceRoomSlots.FirstOrDefault();
            var room = roomSlot?.Room;
            var roomName = room != null ? $"{room.Building} {room.Floor} {room.Name}" : "未指定";
            var slotDate = roomSlot?.SlotDate.ToString("yyyy/MM/dd") ?? "-";
            var slotTime = reservation.ConferenceRoomSlots.Any()
                ? $"{reservation.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ {reservation.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm}"
                : "-";
            var bookingNo = reservation.Id.ToString().Substring(0, 8);
            var paymentDeadline = reservation.PaymentDeadline?.ToString("yyyy/MM/dd") ?? "-";
            var paymentMethod = reservation.PaymentMethod switch
            {
                "transfer" => "銀行匯款",
                "cash" => "現金繳費",
                "cost-sharing" => "成本分攤",
                _ => reservation.PaymentMethod ?? "-"
            };

            var mail = NewMailMessage();
            mail.Subject = $"[繳費期限提醒] - {reservation.Name}";
            mail.Body = BuildPaymentDeadlineReminderBody(
                applicantName,
                bookingNo,
                reservation.Name,
                reservation.OrganizerUnit,
                reservation.Chairman,
                roomName,
                slotDate,
                slotTime,
                reservation.TotalAmount,
                paymentDeadline,
                paymentMethod,
                daysUntilDeadline
            );
            mail.To.Add(applicantEmail);

            Console.WriteLine($"📧 [PaymentDeadlineReminder] 準備寄信給預約者: {applicantEmail}");
            try
            {
                SendAsync(mail, className, functionName).GetAwaiter().GetResult();
                Console.WriteLine($"📧 [PaymentDeadlineReminder] ✅ 信件已發送: {applicantEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📧 [PaymentDeadlineReminder] ❌ 信件發送失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 建立繳費期限提醒郵件內容
        /// </summary>
        private string BuildPaymentDeadlineReminderBody(
            string applicantName,
            string bookingNo,
            string conferenceName,
            string organizerUnit,
            string chairman,
            string roomName,
            string slotDate,
            string slotTime,
            int totalAmount,
            string paymentDeadline,
            string paymentMethod,
            int daysUntilDeadline)
        {
            var urgencyText = daysUntilDeadline switch
            {
                0 => "今天是繳費期限的最後一天",
                1 => "繳費期限只剩 1 天",
                _ => $"繳費期限只剩 {daysUntilDeadline} 天"
            };

            var urgencyColor = daysUntilDeadline <= 1 ? "#dc3545" : "#ffc107";

            return $@"
<h3>親愛的 {applicantName} 您好：</h3>

<div style='background-color: {(daysUntilDeadline <= 1 ? "#f8d7da" : "#fff3cd")}; border-left: 4px solid {urgencyColor}; padding: 15px; margin: 15px 0;'>
    <h4 style='margin: 0; color: {(daysUntilDeadline <= 1 ? "#721c24" : "#856404")};'>⚠️ {urgencyText}！</h4>
    <p style='margin: 10px 0 0 0;'>請盡快完成繳費，逾期預約將自動取消。</p>
</div>

<h4>📋 預約資訊</h4>
<table style='border-collapse: collapse; width: 100%; max-width: 500px;'>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5; width: 120px;'><strong>預約單號</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{bookingNo}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議名稱</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{conferenceName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>承辦單位</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{organizerUnit ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議主席</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{chairman ?? "-"}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>會議室</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{roomName}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約日期</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotDate}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>預約時間</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{slotTime}</td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>應繳金額</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'><strong style='color: #dc3545;'>NT$ {totalAmount:N0}</strong></td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>繳費期限</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'><strong style='color: #dc3545;'>{paymentDeadline}</strong></td>
    </tr>
    <tr>
        <td style='padding: 8px; border: 1px solid #ddd; background-color: #f5f5f5;'><strong>付款方式</strong></td>
        <td style='padding: 8px; border: 1px solid #ddd;'>{paymentMethod}</td>
    </tr>
</table>

<p style='margin-top: 20px;'>
    <a href='{BaseUrl}ReservationOverview' style='display: inline-block; padding: 10px 20px; background-color: #dc3545; color: white; text-decoration: none; border-radius: 5px;'>
        立即前往繳費
    </a>
</p>

<p style='color: #666; margin-top: 30px;'>
    如有任何問題，請聯繫場地管理單位。
</p>
";
        }
    }
}
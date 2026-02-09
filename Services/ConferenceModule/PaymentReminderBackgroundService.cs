using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Models.Enums;
using TASA.Program;

namespace TASA.Services.ConferenceModule
{
    /// <summary>
    /// 繳費期限提醒背景服務
    /// 每天檢查一次，只在繳費期限剩餘 3 天和 1 天時發送提醒
    /// </summary>
    public class PaymentReminderBackgroundService(
        IDbContextFactory<TASAContext> dbContextFactory,
        IServiceScopeFactory scopeFactory) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 等待應用程式啟動完成
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndSendReminders();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PaymentReminderBackgroundService] 錯誤: {ex.Message}");
                }

                // 每天執行一次（每 24 小時）
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task CheckAndSendReminders()
        {
            Console.WriteLine($"[PaymentReminderBackgroundService] 開始檢查繳費期限提醒 - {DateTime.Now:yyyy/MM/dd HH:mm:ss}");

            using var scope = scopeFactory.CreateScope();
            var conferenceMail = scope.ServiceProvider.GetRequiredService<ServiceWrapper>().ConferenceMail;
            using var db = dbContextFactory.CreateDbContext();

            var today = DateTime.Today;
            var threeDaysLater = today.AddDays(3);  // 3 天後到期
            var oneDayLater = today.AddDays(1);     // 1 天後到期

            // 找出需要提醒的預約：
            // 1. 狀態為待繳費
            // 2. 付款狀態不是已繳費
            // 3. 繳費期限剛好是 3 天後或 1 天後
            var reservationsToRemind = await db.Conference
                .Include(c => c.CreateByNavigation)
                .Include(c => c.ConferenceRoomSlots)
                    .ThenInclude(s => s.Room)
                .Where(c => c.DeleteAt == null
                         && c.ReservationStatus == ReservationStatus.PendingPayment
                         && c.PaymentStatus != PaymentStatus.Paid
                         && c.PaymentDeadline.HasValue
                         && (c.PaymentDeadline.Value.Date == threeDaysLater || c.PaymentDeadline.Value.Date == oneDayLater))
                .ToListAsync();

            Console.WriteLine($"[PaymentReminderBackgroundService] 找到 {reservationsToRemind.Count} 筆符合條件的預約");

            var sentCount = 0;

            foreach (var reservation in reservationsToRemind)
            {
                try
                {
                    var daysUntilDeadline = (reservation.PaymentDeadline!.Value.Date - today).Days;

                    // 檢查是否已經發送過這個階段的提醒
                    // 如果今天已經發送過提醒，就跳過
                    if (reservation.PaymentReminderSentAt.HasValue &&
                        reservation.PaymentReminderSentAt.Value.Date == today)
                    {
                        Console.WriteLine($"[PaymentReminderBackgroundService] 跳過（今天已發送）: {reservation.Name}");
                        continue;
                    }

                    // 如果是 3 天提醒，檢查是否之前已發送過 3 天提醒
                    if (daysUntilDeadline == 3 && reservation.PaymentReminderSentAt.HasValue)
                    {
                        var lastReminderDays = (reservation.PaymentDeadline!.Value.Date - reservation.PaymentReminderSentAt.Value.Date).Days;
                        if (lastReminderDays == 3)
                        {
                            Console.WriteLine($"[PaymentReminderBackgroundService] 跳過（3天提醒已發送）: {reservation.Name}");
                            continue;
                        }
                    }

                    Console.WriteLine($"[PaymentReminderBackgroundService] 發送提醒: {reservation.Name}, 剩餘 {daysUntilDeadline} 天");

                    conferenceMail.PaymentDeadlineReminder(reservation.Id, daysUntilDeadline);

                    // 更新提醒發送時間
                    reservation.PaymentReminderSentAt = DateTime.Now;
                    sentCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PaymentReminderBackgroundService] 發送提醒失敗: {reservation.Name} - {ex.Message}");
                }
            }

            if (sentCount > 0)
            {
                await db.SaveChangesAsync();
            }

            Console.WriteLine($"[PaymentReminderBackgroundService] 檢查完成，發送 {sentCount} 封提醒 - {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
        }
    }
}

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using TASA.Models;
using ConferenceStatusEnum = TASA.Models.Enums.ConferenceStatus;  // 用別名避免衝突
using ReservationStatusEnum = TASA.Models.Enums.ReservationStatus;
using SlotStatusEnum = TASA.Models.Enums.SlotStatus;

namespace TASA.Services
{
    public class ReservationAutoManagementService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReservationAutoManagementService> _logger;
        private int _executionCount = 0;

        public ReservationAutoManagementService(
            IServiceProvider serviceProvider,
            ILogger<ReservationAutoManagementService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 啟動時先執行一次
            await UpdateConferenceStatuses();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _executionCount++;

                    // 每分鐘更新會議狀態(即將開始、進行中、休息中、已結束)
                    await UpdateConferenceStatuses();

                    // 每 4 次(4分鐘)檢查一次繳費逾期
                    if (_executionCount % 4 == 0)
                    {
                        await CheckAndCancelOverduePayments();
                    }

                    // 每 1 分鐘執行一次
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "自動管理預約時發生錯誤");
                }
            }
        }

        /// <summary>
        /// 更新會議狀態(即將開始、進行中、休息中、已結束)
        /// </summary>
        private async Task UpdateConferenceStatuses()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TASAContext>();

            var now = DateTime.Now;

            // 只處理預約成功的會議
            var activeConferences = await dbContext.Conference
                .Include(c => c.ConferenceRoomSlots)
                .Where(c => c.ReservationStatus == ReservationStatusEnum.Confirmed)  // 預約成功
                .Where(c => c.Status == null || c.Status <= 3)  // 排除已結束的
                .ToListAsync();

            int updated = 0;

            foreach (var conference in activeConferences)
            {
                var slots = conference.ConferenceRoomSlots
                    .OrderBy(s => s.SlotDate)
                    .ThenBy(s => s.StartTime)
                    .ToList();

                if (!slots.Any()) continue;

                // 取得所有時段的絕對時間
                var slotTimes = slots.Select(s => new
                {
                    Start = s.SlotDate.ToDateTime(s.StartTime),
                    End = s.SlotDate.ToDateTime(s.EndTime)
                }).ToList();

                var firstSlotStart = slotTimes.First().Start;
                var lastSlotEnd = slotTimes.Last().End;

                byte? newStatus = null;

                // 判斷狀態
                if (now >= lastSlotEnd)
                {
                    // 所有時段都結束了 → 已完成
                    newStatus = (byte)ConferenceStatusEnum.Completed;

                    // 更新實際結束時間
                    if (conference.FinishTime == null)
                    {
                        conference.FinishTime = lastSlotEnd;
                    }
                }
                else if (now >= firstSlotStart.AddMinutes(-10) && now < firstSlotStart)
                {
                    // 開始前 10 分鐘 → 已排程(即將開始)
                    newStatus = (byte)ConferenceStatusEnum.Scheduled;
                }
                else
                {
                    // 檢查是否在任何時段內
                    bool isInAnySlot = slotTimes.Any(st => now >= st.Start && now < st.End);

                    if (isInAnySlot)
                    {
                        // 進行中
                        newStatus = (byte)ConferenceStatusEnum.InProgress;
                    }
                    else if (now >= firstSlotStart && now < lastSlotEnd)
                    {
                        // 在時段之間的空檔 → 已排程(休息中)
                        newStatus = (byte)ConferenceStatusEnum.Scheduled;
                    }
                    else
                    {
                        // 還沒開始 → 已排程
                        newStatus = (byte)ConferenceStatusEnum.Scheduled;
                    }
                }

                // 只在狀態改變時更新
                if (conference.Status != newStatus)
                {
                    conference.Status = newStatus;
                    updated++;
                }
            }

            if (updated > 0)
            {
                await dbContext.SaveChangesAsync();
                _logger.LogInformation($"[會議狀態] 更新 {updated} 筆會議狀態");
            }
        }

        /// <summary>
        /// 檢查並取消繳費逾期的預約
        /// </summary>
        private async Task CheckAndCancelOverduePayments()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TASAContext>();

            var now = DateTime.Now;

            // ReservationStatus = PendingPayment 是待繳費
            var overdueReservations = await dbContext.Conference
                .Include(c => c.ConferenceRoomSlots)
                .Include(c => c.ConferenceEquipments)
                .Where(c => c.PaymentDeadline.HasValue
                         && c.PaymentDeadline < now
                         && c.ReservationStatus == ReservationStatusEnum.PendingPayment)  // 待繳費
                .ToListAsync();

            foreach (var conference in overdueReservations)
            {
                // 釋放時段
                foreach (var slot in conference.ConferenceRoomSlots)
                {
                    slot.SlotStatus = (byte)SlotStatusEnum.Available;  // 可預約
                    slot.ReleasedAt = now;
                }

                // 釋放設備狀態
                foreach (var equipment in conference.ConferenceEquipments)
                {
                    equipment.EquipmentStatus = 0;  // 可用
                    equipment.ReleasedAt = now;
                }

                // 更新預約狀態為「已取消」
                conference.ReservationStatus = ReservationStatusEnum.Cancelled;
                conference.CancelledAt = now;
                conference.RejectReason = $"繳費期限 {conference.PaymentDeadline:yyyy-MM-dd HH:mm} 已過期,系統自動取消";
            }

            if (overdueReservations.Any())
            {
                await dbContext.SaveChangesAsync();
                _logger.LogInformation($"[繳費逾期] 自動取消 {overdueReservations.Count} 筆預約");
            }
        }
    }
}
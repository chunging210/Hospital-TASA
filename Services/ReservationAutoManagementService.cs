using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using TASA.Models;
using ConferenceStatusEnum = TASA.Models.Enums.ConferenceStatus;
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

            _logger.LogInformation("🚀 預約自動管理服務啟動");

            // 啟動時先執行一次
            await UpdateConferenceStatuses();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _executionCount++;
                    _logger.LogInformation($"⏰ 第 {_executionCount} 次執行");
                    // 每分鐘更新會議狀態
                    await UpdateConferenceStatuses();

                    // 每 4 次(4分鐘)檢查一次繳費逾期
                    if (_executionCount % 4 == 0)
                    {
                        await CheckAndCancelOverduePayments();
                    }

                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "自動管理預約時發生錯誤");
                }
            }
        }

        private async Task UpdateConferenceStatuses()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TASAContext>();

            var now = DateTime.Now;

            // ✅ 加上 IgnoreQueryFilters()
            var activeConferences = await dbContext.Conference
      .IgnoreQueryFilters()  // ← Background Service 不需要過濾
      .Include(c => c.ConferenceRoomSlots)
      .Where(c => c.ReservationStatus == ReservationStatusEnum.Confirmed)
      .Where(c => c.Status == null || c.Status <= 3)
      .ToListAsync();
            _logger.LogInformation($"📊 找到 {activeConferences.Count} 筆需檢查的會議");
            int updated = 0;

            foreach (var conference in activeConferences)
            {
                var slots = conference.ConferenceRoomSlots
                    .OrderBy(s => s.SlotDate)
                    .ThenBy(s => s.StartTime)
                    .ToList();

                if (!slots.Any()) continue;

                var slotTimes = slots.Select(s => new
                {
                    Start = s.SlotDate.ToDateTime(s.StartTime),
                    End = s.SlotDate.ToDateTime(s.EndTime)
                }).ToList();

                var firstSlotStart = slotTimes.First().Start;
                var lastSlotEnd = slotTimes.Last().End;

                byte? newStatus = null;

                if (now >= lastSlotEnd)
                {
                    newStatus = (byte)ConferenceStatusEnum.Completed;
                    if (conference.FinishTime == null)
                    {
                        conference.FinishTime = lastSlotEnd;
                    }
                }
                else if (now >= firstSlotStart.AddMinutes(-10) && now < firstSlotStart)
                {
                    newStatus = (byte)ConferenceStatusEnum.Scheduled;
                }
                else
                {
                    bool isInAnySlot = slotTimes.Any(st => now >= st.Start && now < st.End);

                    if (isInAnySlot)
                    {
                        newStatus = (byte)ConferenceStatusEnum.InProgress;
                    }
                    else if (now >= firstSlotStart && now < lastSlotEnd)
                    {
                        newStatus = (byte)ConferenceStatusEnum.Scheduled;
                    }
                    else
                    {
                        newStatus = (byte)ConferenceStatusEnum.Scheduled;
                    }
                }

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

        private async Task CheckAndCancelOverduePayments()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TASAContext>();

            var now = DateTime.Now;

            // ✅ 加上 IgnoreQueryFilters()
            var overdueReservations = await dbContext.Conference
                .IgnoreQueryFilters()  // ← 重點!
                .Include(c => c.ConferenceRoomSlots)
                .Include(c => c.ConferenceEquipments)
                .Where(c => c.PaymentDeadline.HasValue
                         && c.PaymentDeadline < now
                         && c.ReservationStatus == ReservationStatusEnum.PendingPayment)
                .ToListAsync();

            _logger.LogInformation($"📊 找到 {overdueReservations.Count} 筆逾期預約");

            foreach (var conference in overdueReservations)
            {
                foreach (var slot in conference.ConferenceRoomSlots)
                {
                    slot.SlotStatus = (byte)SlotStatusEnum.Available;
                    slot.ReleasedAt = now;
                }

                foreach (var equipment in conference.ConferenceEquipments)
                {
                    equipment.EquipmentStatus = 0;
                    equipment.ReleasedAt = now;
                }

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
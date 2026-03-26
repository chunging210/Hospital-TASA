using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using TASA.Models;
using TASA.Program;
using ConferenceStatusEnum = TASA.Models.Enums.ConferenceStatus;
using ReservationStatusEnum = TASA.Models.Enums.ReservationStatus;
using SlotStatusEnum = TASA.Models.Enums.SlotStatus;

namespace TASA.Services
{
    public class ReservationAutoManagementService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReservationAutoManagementService> _logger;
        private DateTime _lastOverdueCheck = DateTime.MinValue;

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
            await CheckAndCancelOverduePayments();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 每分鐘更新會議狀態
                    await UpdateConferenceStatuses();

                    // 每天凌晨檢查一次繳費逾期（檢查日期是否變更）
                    if (_lastOverdueCheck.Date < DateTime.Now.Date)
                    {
                        await CheckAndCancelOverduePayments();
                        _lastOverdueCheck = DateTime.Now;
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
            var serviceWrapper = scope.ServiceProvider.GetRequiredService<ServiceWrapper>();

            var now = DateTime.Now;

            // 繳費期限改為會議結束後 N 天，不再自動取消逾期預約
            _logger.LogInformation("📊 [繳費逾期自動取消] 已停用，略過");

            var cancelledIds = new List<Guid>();

            if (false) // 停用自動取消
            {
            }
        }
    }
}
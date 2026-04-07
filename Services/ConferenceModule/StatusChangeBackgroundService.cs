using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Models.Enums;
using TASA.Program;

namespace TASA.Services.ConferenceModule
{
    public class StatusChangeBackgroundService(IDbContextFactory<TASAContext> dbContextFactory, IServiceScopeFactory scopeFactory) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ServiceWrapper>();
            while (!stoppingToken.IsCancellationRequested)
            {
                Status2(service);
                Status3();
                Status4();
                NewSystemStatus(service);
                await Task.Delay(Cron.GetDelayMilliseconds("* * * * *"), stoppingToken);
            }
        }

        private void Log(int status, string name)
        {
            var newLogBackground = new LogBackground
            {
                Time = DateTime.Now,
                InfoType = "status_change",
                Info = $"Status=>{status}|{name}"
            };
            Task.Run(() =>
            {
                using var db = dbContextFactory.CreateDbContext();
                db.LogBackground.Add(newLogBackground);
                db.SaveChanges();
            });
        }

        private void Status2(ServiceWrapper service)
        {
            var preparationTime = DateTime.Now.AddMinutes(service.SettingServices.GetSettings().UCNS.BeforeStart);
            using var db = dbContextFactory.CreateDbContext();
            var conference = db.Conference
                    .WhereNotDeleted()
                    .Where(x => preparationTime >= x.StartTime && x.Status == 1)
                    .ToList();
            foreach (var item in conference)
            {
                item.Status = 2;
                Log(2, item.Name);
            }
            db.SaveChanges();
        }

        private void Status3()
        {
            using var db = dbContextFactory.CreateDbContext();
            var conference = db.Conference
                    .WhereNotDeleted()
                    .Where(x => DateTime.Now >= x.StartTime && (x.Status == 1 || x.Status == 2))
                    .ToList();
            foreach (var item in conference)
            {
                item.Status = 3;
                Log(3, item.Name);
            }
            db.SaveChanges();
        }

        private void Status4()
        {
            using var db = dbContextFactory.CreateDbContext();
            var conference = db.Conference
                    .WhereNotDeleted()
                    .Where(x => DateTime.Now >= (x.FinishTime ?? x.EndTime) && (x.Status == 1 || x.Status == 2 || x.Status == 3))
                    .ToList();
            foreach (var item in conference)
            {
                item.Status = 4;
                Log(4, item.Name);
            }
            db.SaveChanges();
        }

        // 新預約系統：依 ConferenceRoomSlot 時段更新 Status
        private void NewSystemStatus(ServiceWrapper service)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var now = TimeOnly.FromDateTime(DateTime.Now);
            var preparationMinutes = service.SettingServices.GetSettings().UCNS.BeforeStart;
            var preparationTime = now.AddMinutes(preparationMinutes);

            using var db = dbContextFactory.CreateDbContext();
            var conferences = db.Conference
                .Include(x => x.ConferenceRoomSlots.Where(s => s.SlotDate == today))
                .WhereNotDeleted()
                .Where(x => !x.StartTime.HasValue)
                .Where(x => x.ReservationStatus == ReservationStatus.Confirmed)
                .Where(x => x.ConferenceRoomSlots.Any(s => s.SlotDate == today))
                .ToList();

            foreach (var conference in conferences)
            {
                var todaySlots = conference.ConferenceRoomSlots.ToList();
                if (!todaySlots.Any()) continue;

                var minStart = todaySlots.Min(s => s.StartTime);
                var maxEnd = todaySlots.Max(s => s.EndTime);

                byte newStatus;
                if (now >= maxEnd)
                    newStatus = 3;
                else if (preparationTime >= minStart)
                    newStatus = 2;
                else
                    newStatus = 1;

                if (conference.Status != newStatus)
                {
                    conference.Status = newStatus;
                    Log(newStatus, conference.Name);
                }
            }
            db.SaveChanges();
        }
    }
}
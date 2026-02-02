using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
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
                await Task.Delay(Cron.GetDelayMilliseconds("* * * * *"), stoppingToken);
            }
        }

        private void Log(int status, string name)
        {
            var newLogBackground = new LogBackground
            {
                Time = DateTime.Now,
                InfoType = "StatusChange",
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
                    .Include(x => x.Ecs)
                    .WhereNotDeleted()
                    .Where(x => preparationTime >= x.StartTime && x.Status == 1)
                    .ToList();
            foreach (var item in conference)
            {
                item.Status = 2;
                Log(2, item.Name);
            }
            db.SaveChanges();
            foreach (var item in conference)
            {
                service.JobService.DoEcs(item);
            }
        }

        private void Status3()
        {
            using var db = dbContextFactory.CreateDbContext();
            var conference = db.Conference
                    .Include(x => x.ConferenceWebex)
                    .Include(x => x.Room)
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
                    .Include(x => x.ConferenceWebex)
                    .Include(x => x.Room)
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
    }
}
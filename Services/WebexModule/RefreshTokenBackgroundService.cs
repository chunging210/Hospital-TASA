using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using TASA.Services.WebexModule;

namespace TASA.Services.ConferenceModule
{
    public class RefreshTokenBackgroundService(IServiceScopeFactory scopeFactory) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TASAContext>();
            while (!stoppingToken.IsCancellationRequested)
            {
                Refresh(db);
                await Task.Delay(Cron.GetDelayMilliseconds("0 0 * * *"), stoppingToken);
            }
        }

        //private void Log(int status, string name)
        //{
        //    var newLogBackground = new LogWebex
        //    {
        //        Time = DateTime.UtcNow,
        //        InfoType = "StatusChange",
        //        Info = $"Status=>{status}|{name}"
        //    };
        //    Task.Run(() =>
        //    {
        //        using var db = dbContextFactory.CreateDbContext();
        //        db.LogBackground.Add(newLogBackground);
        //        db.SaveChanges();
        //    });
        //}

        private void Refresh(TASAContext db)
        {
            var needRefreshTime = DateTime.UtcNow.AddDays(3);
            var webex = db.Webex
                .WhereNotDeleted()
                .WhereEnabled()
                .Where(x => x.Expires <= needRefreshTime)
                .ToList();
            using var webexclient = new WebexHttpClient();
            foreach (var item in webex)
            {
                var response = webexclient.RefreshToken(item.Client_id, item.Client_secret, item.Refresh_token);
                try
                {
                    var access = System.Text.Json.JsonSerializer.Deserialize<TokenVM>(response, new System.Text.Json.JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                    if (access?.Errors?.Count == null || access?.Errors?.Count == 0)
                    {
                        item.Access_token = access?.Access_token ?? "";
                        item.Expires = DateTime.UtcNow.AddSeconds(access?.Expires_in ?? 0);
                        item.Refresh_token = access?.Refresh_token ?? "";
                        item.Refresh_token_expires = DateTime.UtcNow.AddSeconds(access?.Refresh_token_expires_in ?? 0);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.GetBaseException().Message);
                }
            }
            db.SaveChanges();
        }
    }
}
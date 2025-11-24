using Microsoft.EntityFrameworkCore;
using TASA.Models;
using TASA.Program;
using TASA.Services.AuthModule;

namespace TASA.Services
{
    public class LogServices(IDbContextFactory<TASAContext> dbContextFactory, IHttpContextAccessor httpContextAccessor, UserClaimsService userClaimsService) : IService
    {
        public static async Task LogAsync(TASAContext db, string ip, string infoType, string info, Guid? userId)
        {
            var newSysLog = new LogSys
            {
                Ip = ip,
                Time = DateTime.Now,
                InfoType = infoType,
                Info = info,
                UserId = userId
            };
            db.LogSys.Add(newSysLog);
            await db.SaveChangesAsync();
        }

        public async Task LogAsync(string infoType, string info)
        {
            var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "";
            var userId = userClaimsService.Me()?.Id;
            await using var db = dbContextFactory.CreateDbContext();
            await LogAsync(db, ip, infoType, info, userId);
        }
    }
}
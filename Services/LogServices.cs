using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TASA.Models;
using TASA.Program;
using TASA.Services.AuthModule;

namespace TASA.Services
{
    public class LogServices(IDbContextFactory<TASAContext> dbContextFactory, IHttpContextAccessor httpContextAccessor, UserClaimsService userClaimsService) : IService
    {
        /// <summary>
        /// 確保 info 為 JSON 格式。若傳入純字串，自動包成 {"ActionDescription":"..."} 格式。
        /// </summary>
        private static string EnsureJson(string info)
        {
            if (string.IsNullOrEmpty(info))
                return JsonConvert.SerializeObject(new { ActionDescription = "" });
            var trimmed = info.Trim();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                return info;
            return JsonConvert.SerializeObject(new { ActionDescription = info });
        }

        public static async Task LogAsync(TASAContext db, string ip, string infoType, string info, Guid? userId, Guid? departmentId)
        {
            var newSysLog = new LogSys
            {
                Ip = ip,
                Time = DateTime.Now,
                InfoType = infoType,
                Info = EnsureJson(info),
                UserId = userId,
                DepartmentId = departmentId
            };
            db.LogSys.Add(newSysLog);
            await db.SaveChangesAsync();
        }

        public async Task LogAsync(string infoType, string info)
        {
            var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "";
            var me = userClaimsService.Me();
            var userId = me?.Id;
            var departmentId = me?.DepartmentId;
            await using var db = dbContextFactory.CreateDbContext();
            await LogAsync(db, ip, infoType, info, userId, departmentId);
        }

        public async Task LogAsync(string infoType, string info, Guid? userId, Guid? departmentId)
        {
            var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "";
            await using var db = dbContextFactory.CreateDbContext();
            await LogAsync(db, ip, infoType, info, userId, departmentId);
        }
    }
}
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using System.Text.RegularExpressions;

namespace TASA.Services.AuthUserModule
{
    public class ProfilesServices(TASAContext db, ServiceWrapper service, IHttpContextAccessor httpContextAccessor) : IService
    {
        // ✅ 取得客戶端 IP
        private string GetClientIp()
        {
            var context = httpContextAccessor.HttpContext;
            var ip = context?.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim()
                ?? context?.Request.Headers["X-Real-IP"].ToString()
                ?? context?.Connection.RemoteIpAddress?.ToString()
                ?? "未知";
            return ip;
        }

        // ✅ 取得瀏覽器和裝置資訊
        private (string browser, string device) GetDeviceInfo()
        {
            var context = httpContextAccessor.HttpContext;
            var userAgent = context?.Request.Headers["User-Agent"].ToString() ?? "未知";

            var device = "未知";
            var browser = "未知";

            if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
                device = "Windows";
            else if (userAgent.Contains("Mac", StringComparison.OrdinalIgnoreCase))
                device = "macOS";
            else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
                device = "Linux";
            else if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
                device = "iPhone";
            else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
                device = "Android";

            if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase) && !userAgent.Contains("Chromium"))
                browser = "Chrome";
            else if (userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase) && !userAgent.Contains("Chrome"))
                browser = "Safari";
            else if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
                browser = "Firefox";
            else if (userAgent.Contains("Edge", StringComparison.OrdinalIgnoreCase))
                browser = "Edge";

            return (browser, device);
        }

        public record DetailVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? Password { get; set; }
        }
        public DetailVM? Detail()
        {
            var userid = service.UserClaimsService.Me()?.Id;
            return db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .Mapping(x => new DetailVM()
                {
                    Password = ""
                })
                .FirstOrDefault(x => x.Id == userid);
        }

        public bool IsValidPassword(string password)
        {
            string regexPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{10,}$";
            return Regex.IsMatch(password, regexPattern);
        }

        public void Update(DetailVM vm)
        {
            var user = db.AuthUser
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);
            if (user != null)
            {
                var oldName = user.Name;
                var oldEmail = user.Email;

                user.Name = vm.Name;
                user.Email = vm.Email;

                // ✅ 記錄個人資料修改
                if (oldName != vm.Name || oldEmail != vm.Email)
                {
                    var deviceInfo = GetDeviceInfo();
                    var profileUpdateInfo = new
                    {
                        OperatorName = user.Name,  // 自己修改自己
                        TargetName = user.Name,
                        UserName = user.Account,
                        Action = "update_profile",
                        OldName = oldName,
                        NewName = vm.Name,
                        OldEmail = oldEmail,
                        NewEmail = vm.Email,
                        IsSuccess = true,
                        ClientIp = GetClientIp(),
                        DeviceInfo = deviceInfo.device,
                        BrowserInfo = deviceInfo.browser,
                        Timestamp = DateTime.Now
                    };
                    _ = service.LogServices.LogAsync("user_update_profile", JsonConvert.SerializeObject(profileUpdateInfo), user.Id, user.DepartmentId);
                }

                if (!string.IsNullOrEmpty(vm.Password))
                {
                    if (!IsValidPassword(vm.Password))
                    {
                        throw new HttpException("密碼不符合規則");
                    }
                    // ✅ 記錄變更密碼
                    var deviceInfo = GetDeviceInfo();
                    var passwordUpdateInfo = new
                    {
                        OperatorName = user.Name,  // 自己修改自己
                        TargetName = user.Name,
                        UserName = user.Account,
                        Action = "change_password",
                        IsSuccess = true,
                        ClientIp = GetClientIp(),
                        DeviceInfo = deviceInfo.device,
                        BrowserInfo = deviceInfo.browser,
                        Timestamp = DateTime.Now
                    };
                    _ = service.LogServices.LogAsync("user_update_password", JsonConvert.SerializeObject(passwordUpdateInfo), user.Id, user.DepartmentId);
                    user.Password = vm.Password;
                }
                db.SaveChanges();
            }
        }
    }
}

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using Newtonsoft.Json;

namespace TASA.Services.AuthModule
{
    public class LoginServices(TASAContext db, ServiceWrapper service, IHttpContextAccessor httpContextAccessor) : IService
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

        // ✅ 取得登入方式
        private string GetLoginMethod(string? provider = null)
        {
            return provider switch
            {
                "Google" => "Google",
                "Microsoft" => "Microsoft",
                "Facebook" => "Facebook",
                _ => "本地"
            };
        }

        // ✅ 取得瀏覽器和裝置資訊 - 回傳 tuple
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

        public AuthUser? IsValidUser(string account, string password)
        {
            var user = db.AuthUser
                .Include(x => x.AuthRole)
                .Include(x => x.Department)
                .AsNoTracking()
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Account == account);
            if (user == null || !HashString.Verify(password, user.PasswordHash, user.PasswordSalt))
            {
                return null;
            }
            IsEnabled(user);
            return user;
        }

        public void IsEnabled(AuthUser? user)
        {
            if (user?.IsEnabled == false)
            {
                var deviceInfo = GetDeviceInfo();
                var failureInfo = new 
                { 
                    UserName = user.Account, 
                    LoginMethod = GetLoginMethod(),
                    IsSuccess = false, 
                    FailureReason = "帳號已停用",
                    ClientIp = GetClientIp(),
                    DeviceInfo = deviceInfo.device,
                    BrowserInfo = deviceInfo.browser,
                    Timestamp = DateTime.Now
                };
                _ = service.LogServices.LogAsync("login_failed", JsonConvert.SerializeObject(failureInfo), user.Id, user.DepartmentId);
                throw new HttpException("帳號已停用")
                {
                    StatusCode = System.Net.HttpStatusCode.Unauthorized
                };
            }
        }

        public record LoginVM
        {
            [Required(ErrorMessage = "帳號是必要項")]
            public string Account { get; set; } = string.Empty;
            [Required(ErrorMessage = "密碼是必要項")]
            public string Password { get; set; } = string.Empty;
        }

        public AuthUser Login(LoginVM vm)
        {
            var user = IsValidUser(vm.Account, vm.Password);
            if (user == null)
            {
                var deviceInfo = GetDeviceInfo();
                var failureInfo = new 
                { 
                    UserName = vm.Account, 
                    LoginMethod = GetLoginMethod(),
                    IsSuccess = false, 
                    FailureReason = "密碼錯誤",
                    ClientIp = GetClientIp(),
                    DeviceInfo = deviceInfo.device,
                    BrowserInfo = deviceInfo.browser,
                    Timestamp = DateTime.Now
                };
                _ = service.LogServices.LogAsync("login_failed", JsonConvert.SerializeObject(failureInfo));
                throw new HttpException("登入失敗")
                {
                    StatusCode = System.Net.HttpStatusCode.Unauthorized
                };
            }
            else
            {
                var deviceInfo = GetDeviceInfo();
                var successInfo = new 
                { 
                    UserName = user.Account,
                    LoginMethod = GetLoginMethod(),
                    IsSuccess = true,
                    ClientIp = GetClientIp(),
                    DeviceInfo = deviceInfo.device,
                    BrowserInfo = deviceInfo.browser,
                    Timestamp = DateTime.Now
                };
                _ = service.LogServices.LogAsync("login_success", JsonConvert.SerializeObject(successInfo), user.Id, user.DepartmentId);
                return user;
            }
        }

public void GenerateCookie(IResponseCookies cookies, AuthUser user)
{
    // 查詢使用者是否為會議室管理者
    var isRoomManager = db.SysRoom
        .AsNoTracking()
        .IgnoreQueryFilters()
        .Any(r => r.ManagerId == user.Id
               && r.IsEnabled
               && r.DeleteAt == null);

    // 檢查是否為有效的代理人
    if (!isRoomManager)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        isRoomManager = db.RoomManagerDelegate
            .AsNoTracking()
            .Any(d => d.DelegateUserId == user.Id
                   && d.IsEnabled
                   && d.DeleteAt == null
                   && d.StartDate <= today
                   && d.EndDate >= today);
    }

    var claims = UserClaimsService.ToClaims(user, isRoomManager);
    
    Jwt.GenerateCookie(cookies, claims);
}

        public void DeleteCookie(IResponseCookies cookies)
        {
            Jwt.DeleteCookie(cookies);
        }

        public string RedirectUrl(ClaimsPrincipal? user)
        {
            var authUser = UserClaimsService.ToAuthUser(user?.Claims);
            if (authUser != null)
            {
                return "/Auth/Profiles";
            }
            else
            {
                return "/";
            }
        }
    }
}
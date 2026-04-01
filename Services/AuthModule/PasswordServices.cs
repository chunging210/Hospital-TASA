using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.AuthModule
{
    public class PasswordServices(TASAContext db, ServiceWrapper service, IHttpContextAccessor httpContextAccessor) : IService
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

        /// <summary>
        /// 密碼驗證規則：至少8字元，大寫/小寫/數字/特殊符號 4取3
        /// </summary>
        public static bool IsValidPassword(string password)
        {
            if (password.Length < 8) return false;
            int categories = 0;
            if (Regex.IsMatch(password, @"[a-z]")) categories++;
            if (Regex.IsMatch(password, @"[A-Z]")) categories++;
            if (Regex.IsMatch(password, @"\d")) categories++;
            if (Regex.IsMatch(password, @"[@$!%*?&\-_#^]")) categories++;
            return categories >= 3;
        }

        public static string PasswordRuleMessage => "密碼須至少 8 個字元，並包含大寫字母、小寫字母、數字、特殊符號（@$!%*?&-_#^）中的任意三種";

        public void ToHash()
        {
            var user = db.AuthUser.Where(x => string.IsNullOrEmpty(x.PasswordHash)).ToList();
            foreach (var item in user)
            {
                var hashPassword = HashString.Hash(item.Password);
                item.PasswordHash = hashPassword.Hash;
                item.PasswordSalt = hashPassword.Salt;
            }
            db.SaveChanges();
        }

        public record ForgetMailVM
        {
            [Required(ErrorMessage = "帳號是必要項")]
            public string Account { get; set; } = string.Empty;
        }
        public void ForgetMail(ForgetMailVM vm)
        {
            var user = db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Account == vm.Account || x.Email == vm.Account);
            if (user == null)
            {
                throw new HttpException("此帳號或信箱尚未註冊");
            }
            service.LoginServices.IsEnabled(user);
            if (user != null)
            {
                var newAuthForget = new AuthForget()
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ExpiresAt = DateTime.Now.AddMinutes(15),
                    IsUsed = false
                };
                db.AuthForget.Add(newAuthForget);
                db.SaveChanges();
                // 忘記密碼申請只是發信，不記錄在「修改帳號日誌」
                service.PasswordMail.Forget(newAuthForget.Id, newAuthForget.ExpiresAt, user.Email);
            }
        }

        public record ForgetVM
        {
            public Guid Id { get; set; }
            [Required(ErrorMessage = "密碼是必要項")]
            public string Password { get; set; } = string.Empty;
        }
        public void Forget(ForgetVM vm)
        {
            // 密碼規則驗證
            if (!IsValidPassword(vm.Password))
            {
                throw new HttpException(PasswordRuleMessage);
            }

            var forget = db.AuthForget
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);
            if (forget != null)
            {
                if (forget.IsUsed)
                {
                    throw new HttpException("此連結已使用過，請重新申請");
                }
                if (forget.ExpiresAt < DateTime.Now)
                {
                    throw new HttpException("此連結已過期，請重新申請");
                }

                var user = db.AuthUser
                    .WhereNotDeleted()
                    .FirstOrDefault(x => x.Id == forget.UserId);
                if (user != null)
                {
                    // 密碼不得與帳號相同
                    if (vm.Password.Equals(user.Account, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new HttpException("密碼不得與帳號相同");
                    }

                    // 不得沿用歷史密碼
                    CheckSameAsCurrentPassword(user, vm.Password);

                    forget.IsUsed = true;
                    var hashPassword = HashString.Hash(vm.Password);
    
                    user.PasswordHash = hashPassword.Hash;
                    user.PasswordSalt = hashPassword.Salt;

                    user.PasswordChangedAt = DateTime.Now;
                    db.SaveChanges();
                    // ✅ 記錄透過忘記密碼重設密碼（手動傳入 UserId 和 DepartmentId，因為用戶還沒登入）
                    var deviceInfo = GetDeviceInfo();
                    var resetInfo = new
                    {
                        OperatorName = user.Name,  // 自己透過忘記密碼重設
                        TargetName = user.Name,
                        UserName = user.Account,
                        Email = user.Email,
                        Action = "reset_password",
                        IsSuccess = true,
                        ClientIp = GetClientIp(),
                        DeviceInfo = deviceInfo.device,
                        BrowserInfo = deviceInfo.browser,
                        Timestamp = DateTime.Now
                    };
                    _ = service.LogServices.LogAsync("user_update_password", JsonConvert.SerializeObject(resetInfo), user.Id, user.DepartmentId);
                    service.PasswordMail.PasswordChange(user.Email);
                }
            }
        }

        /// <summary>
        /// 檢查新密碼是否與目前密碼相同
        /// </summary>
        private void CheckSameAsCurrentPassword(AuthUser user, string newPassword)
        {
            if (!string.IsNullOrEmpty(user.PasswordHash) &&
                HashString.Verify(newPassword, user.PasswordHash, user.PasswordSalt))
            {
                throw new HttpException("不可使用與目前相同的密碼");
            }
        }

        public record ChangePWVM
        {
            [Required(ErrorMessage = "密碼是必要項")]
            public string Password { get; set; } = string.Empty;
        }
        public void ChangePW(ChangePWVM vm)
        {
            // 密碼規則驗證
            if (!IsValidPassword(vm.Password))
            {
                throw new HttpException(PasswordRuleMessage);
            }

            var userid = service.UserClaimsService.Me()?.Id;
            var user = db.AuthUser
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == userid);
            if (user != null)
            {
                // 密碼不得與帳號相同
                if (vm.Password.Equals(user.Account, StringComparison.OrdinalIgnoreCase))
                {
                    throw new HttpException("密碼不得與帳號相同");
                }

                // 不得沿用歷史密碼
                CheckSameAsCurrentPassword(user, vm.Password);

                var hashPassword = HashString.Hash(vm.Password);


                user.PasswordHash = hashPassword.Hash;
                user.PasswordSalt = hashPassword.Salt;
                user.PasswordChangedAt = DateTime.Now;
                db.SaveChanges();

                // ✅ 記錄變更密碼
                var deviceInfo = GetDeviceInfo();
                var changeInfo = new
                {
                    OperatorName = user.Name,
                    TargetName = user.Name,
                    UserName = user.Account,
                    Email = user.Email,
                    Action = "change_password",
                    IsSuccess = true,
                    ClientIp = GetClientIp(),
                    DeviceInfo = deviceInfo.device,
                    BrowserInfo = deviceInfo.browser,
                    Timestamp = DateTime.Now
                };
                _ = service.LogServices.LogAsync("user_update_password", JsonConvert.SerializeObject(changeInfo), user.Id, user.DepartmentId);
                service.PasswordMail.PasswordChange(user.Email);
            }
        }

        /// <summary>
        /// 測試用：直接重置密碼（不檢查密碼規則，不寄信）
        /// </summary>
        public void DevResetPassword(string account, string password)
        {
            var user = db.AuthUser
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Account == account || x.Email == account);
            if (user == null)
            {
                throw new HttpException("找不到此帳號");
            }
            var hashPassword = HashString.Hash(password);
            user.PasswordHash = hashPassword.Hash;
            user.PasswordSalt = hashPassword.Salt;
            user.PasswordChangedAt = DateTime.Now;
            db.SaveChanges();
        }
    }
}

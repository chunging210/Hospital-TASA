using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using TASA.Services.AuthUserModule;
using TASA.Services.CaptchaModule;
using System.Net;
using Newtonsoft.Json;

namespace TASA.Services.AuthModule
{
    public class RegisterServices(TASAContext db, ServiceWrapper service, IHttpContextAccessor httpContextAccessor, CaptchaService captchaService) : IService
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

        // ✅ 取得瀏覽器和裝置資訊 - 回傳 tuple（和 LoginServices 一樣）
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
        /// 密碼驗證規則：至少10字元，包含大小寫字母、數字、特殊字元
        /// </summary>
        public static bool IsValidPassword(string password)
        {
            string regexPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{10,}$";
            return Regex.IsMatch(password, regexPattern);
        }

        public static string PasswordRuleMessage => "密碼須至少 10 個字元，並包含大寫字母、小寫字母、數字及特殊字元（@$!%*?&）";

        /* ===============================
         * Register VM
         * =============================== */
        public record RegisterVM
        {
            [Required]
            public string Name { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            public string Password { get; set; } = string.Empty;

            [Required]
            public string ConfirmPassword { get; set; } = string.Empty;

            /// <summary>
            /// 分院 Id（null = 一般會員）
            /// </summary>
            public Guid? DepartmentId { get; set; }

            /// <summary>
            /// 驗證碼
            /// </summary>
            [Required]
            public string Captcha { get; set; } = string.Empty;
        }

        /* ===============================
         * Register
         * =============================== */
        public void Register(RegisterVM vm)
        {
            // ✅ 在方法開頭宣告一次，所有區塊共用
            var deviceInfo = GetDeviceInfo();

            // 0️⃣ 驗證碼驗證
            if (!captchaService.Validate(vm.Captcha))
            {
                var failureInfo = new
                {
                    UserName = vm.Email,
                    Email = vm.Email,
                    IsSuccess = false,
                    FailureReason = "驗證碼錯誤",
                    ClientIp = GetClientIp(),
                    DeviceInfo = deviceInfo.device,
                    BrowserInfo = deviceInfo.browser,
                    LoginMethod = GetLoginMethod(),
                    Timestamp = DateTime.Now
                };
                _ = service.LogServices.LogAsync("user_register_failed", JsonConvert.SerializeObject(failureInfo));
                throw new HttpException("驗證碼錯誤，請重新輸入")
                {
                    StatusCode = System.Net.HttpStatusCode.BadRequest
                };
            }

            // 1️⃣ 密碼確認
            if (vm.Password != vm.ConfirmPassword)
            {
                var failureInfo = new
                {
                    UserName = vm.Email,
                    Email = vm.Email,
                    IsSuccess = false,
                    FailureReason = "密碼不符",
                    ClientIp = GetClientIp(),
                    DeviceInfo = deviceInfo.device,
                    BrowserInfo = deviceInfo.browser,
                    LoginMethod = GetLoginMethod(),
                    Timestamp = DateTime.Now
                };
                _ = service.LogServices.LogAsync("user_register_failed", JsonConvert.SerializeObject(failureInfo));
                throw new HttpException("兩次輸入的密碼不一致")
                {
                    StatusCode = System.Net.HttpStatusCode.BadRequest
                };
            }

            // 2️⃣ 密碼規則驗證
            if (!IsValidPassword(vm.Password))
            {
                var failureInfo = new
                {
                    UserName = vm.Email,
                    Email = vm.Email,
                    IsSuccess = false,
                    FailureReason = "密碼不符合規則",
                    ClientIp = GetClientIp(),
                    DeviceInfo = deviceInfo.device,
                    BrowserInfo = deviceInfo.browser,
                    LoginMethod = GetLoginMethod(),
                    Timestamp = DateTime.Now
                };
                _ = service.LogServices.LogAsync("user_register_failed", JsonConvert.SerializeObject(failureInfo));
                throw new HttpException(PasswordRuleMessage)
                {
                    StatusCode = System.Net.HttpStatusCode.BadRequest
                };
            }

            // 3️⃣ 檢查帳號 / Email 是否已存在
            var existingUser = db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Account == vm.Email || x.Email == vm.Email);

            if (existingUser != null)
            {
                // 區分已啟用和等待審核的情況
                var failureReason = existingUser.IsEnabled ? "Email已存在" : "帳號等待審核中";
                var errorMessage = existingUser.IsEnabled
                    ? "此 Email 已被註冊"
                    : "此帳號已註冊，目前正在等待管理者審核，請耐心等候";

                var failureInfo = new
                {
                    UserName = vm.Email,
                    Email = vm.Email,
                    IsSuccess = false,
                    FailureReason = failureReason,
                    ClientIp = GetClientIp(),
                    DeviceInfo = deviceInfo.device,
                    BrowserInfo = deviceInfo.browser,
                    LoginMethod = GetLoginMethod(),
                    Timestamp = DateTime.Now
                };
                // ✅ 傳入已存在用戶的 UserId 和 DepartmentId
                _ = service.LogServices.LogAsync("user_register_failed", JsonConvert.SerializeObject(failureInfo), existingUser.Id, existingUser.DepartmentId);
                throw new HttpException(errorMessage)
                {
                    StatusCode = System.Net.HttpStatusCode.BadRequest
                };
            }

            // 2️⃣ 判斷角色：外院人士 → NORMAL，有選分院 → STAFF
            var isExternal = !vm.DepartmentId.HasValue;
            var roleCode = isExternal ? "NORMAL" : "STAFF";
            var role = db.AuthRole.FirstOrDefault(x => x.Code == roleCode);

            if (role == null)
            {
                throw new HttpException($"系統尚未設定 {roleCode} 角色")
                {
                    StatusCode = System.Net.HttpStatusCode.InternalServerError
                };
            }

            // 3️⃣ 分院
            SysDepartment? department = null;
            if (isExternal)
            {
                // 外院人士：自動設定為臺北總院
                department = db.SysDepartment
                    .AsNoTracking()
                    .WhereNotDeleted()
                    .FirstOrDefault(x => x.Sequence == 1);  // 假設臺北總院 Sequence = 1

                if (department == null)
                {
                    // 備用方案:用名稱搜尋
                    department = db.SysDepartment
                        .AsNoTracking()
                        .WhereNotDeleted()
                        .FirstOrDefault(x => x.Name.Contains("臺北總院"));
                }

                if (department == null)
                {
                    throw new HttpException("無法找到預設分院(臺北總院)");
                }

                Console.WriteLine($"✅ 院外人士註冊,角色: NORMAL,分院: {department.Name}");
            }
            else
            {
                // 內部員工：驗證分院是否存在
                department = db.SysDepartment
                    .AsNoTracking()
                    .FirstOrDefault(x => x.Id == vm.DepartmentId.Value);

                if (department == null)
                {
                    throw new HttpException("所選分院不存在");
                }

                Console.WriteLine($"✅ 內部員工註冊,角色: STAFF,分院: {department.Name}");
            }

            // 4️⃣ 密碼加密
            var hashVm = HashString.Hash(vm.Password);

            // 5️⃣ 建立使用者
            var user = new AuthUser
            {
                Id = Guid.NewGuid(),
                Account = vm.Email,
                Email = vm.Email,
                Name = vm.Name,
                PasswordHash = hashVm.Hash,
                PasswordSalt = hashVm.Salt,
                DepartmentId = department.Id,
                IsEnabled = false,
                CreateAt = DateTime.Now,
            };

            user.AuthRole.Add(role);

            db.AuthUser.Add(user);
            db.SaveChanges();

            // 6️⃣ 紀錄 Log - ✅ 使用最上面宣告的 deviceInfo
            var successInfo = new
            {
                UserName = user.Account,
                Email = user.Email,
                IsSuccess = true,
                ClientIp = GetClientIp(),
                DeviceInfo = deviceInfo.device,
                BrowserInfo = deviceInfo.browser,
                LoginMethod = GetLoginMethod(),
                DepartmentId = user.DepartmentId,
                Timestamp = DateTime.Now
            };
            // ✅ 傳入新建用戶的 UserId 和 DepartmentId
            _ = service.LogServices.LogAsync("user_register_success", JsonConvert.SerializeObject(successInfo), user.Id, user.DepartmentId);

            // 7️⃣ 寄信通知分院主任（DIRECTOR），沒有則寄給 ADMIN
            var directorEmails = db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x => x.IsEnabled && x.DepartmentId == department.Id)
                .Where(x => x.AuthRole.Any(r => r.Code == AuthRoleServices.Director))
                .Select(x => x.Email)
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();

            if (directorEmails.Count == 0)
            {
                directorEmails = db.AuthUser
                    .AsNoTracking()
                    .WhereNotDeleted()
                    .Where(x => x.IsEnabled)
                    .Where(x => x.AuthRole.Any(r => r.Code == AuthRoleServices.Admin))
                    .Select(x => x.Email)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
            }

            service.PasswordMail.RegisterNotify(user.Name, user.Email, department.Name, directorEmails);
        }

    }
}
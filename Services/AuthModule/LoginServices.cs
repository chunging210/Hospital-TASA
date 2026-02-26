using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.DirectoryServices;
using System.Runtime.Versioning;
using System.Security.Claims;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using Newtonsoft.Json;

namespace TASA.Services.AuthModule
{
    [SupportedOSPlatform("windows")]
    public class LoginServices(TASAContext db, ServiceWrapper service, IHttpContextAccessor httpContextAccessor) : IService
    {
        // ===================== AD 設定（寫死） =====================
        private const string AD_PATH = "LDAP://10.99.10.3:389/DC=VGHTPE,DC=GOV,DC=TW";
        private const string AD_DOMAIN = "VGHTPE";
        private static readonly Guid DEFAULT_DEPARTMENT_ID = Guid.Parse("76c5f0c6-e54f-11f0-89ec-9009d062d9e7");
        private static readonly Guid DEFAULT_ROLE_ID = Guid.Parse("397d1cc1-e54f-11f0-89ec-9009d062d9e7");
        private static readonly Guid SYSTEM_USER_ID = Guid.Empty; // CreateBy 用系統帳號

        // ===================== AD LOG 檔案路徑 =====================
        private static readonly string AD_LOG_FOLDER = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private static readonly object _logLock = new object(); // 確保執行緒安全

        /// <summary>
        /// 取得時間戳記格式
        /// </summary>
        private static string GetTimestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        /// <summary>
        /// 寫入單行 LOG
        /// </summary>
        private void WriteLogLine(System.Text.StringBuilder sb, string message, bool isError = false, bool isSuccess = false)
        {
            var prefix = isError ? "✗" : (isSuccess ? "✓" : " ");
            sb.AppendLine($"[{GetTimestamp()}] {prefix} {message}");
        }

        /// <summary>
        /// 寫入 AD LOG 到 txt 檔案
        /// </summary>
        private void WriteAdLog(string logType, string message, string? account = null, string? errorMessage = null)
        {
            try
            {
                if (!Directory.Exists(AD_LOG_FOLDER))
                {
                    Directory.CreateDirectory(AD_LOG_FOLDER);
                }

                var fileName = $"AD_Login_{DateTime.Now:yyyy-MM-dd}.txt";
                var filePath = Path.Combine(AD_LOG_FOLDER, fileName);

                var logEntry = new System.Text.StringBuilder();

                var isError = logType.Contains("FAIL") || logType.Contains("ERROR");
                var isSuccess = logType.Contains("SUCCESS");

                WriteLogLine(logEntry, message, isError, isSuccess);

                lock (_logLock)
                {
                    File.AppendAllText(filePath, logEntry.ToString());
                }
            }
            catch
            {
                // LOG 寫入失敗不應影響主流程
            }
        }

        /// <summary>
        /// 寫入 AD 登入流程開始的 LOG 區塊
        /// </summary>
        private void WriteAdLoginStartLog(string account)
        {
            try
            {
                if (!Directory.Exists(AD_LOG_FOLDER))
                {
                    Directory.CreateDirectory(AD_LOG_FOLDER);
                }

                var fileName = $"AD_Login_{DateTime.Now:yyyy-MM-dd}.txt";
                var filePath = Path.Combine(AD_LOG_FOLDER, fileName);

                var logEntry = new System.Text.StringBuilder();
                logEntry.AppendLine($"[{GetTimestamp()}] ========== 開始 AD 登入驗證 ==========");
                logEntry.AppendLine($"[{GetTimestamp()}]   AD 伺服器: {AD_PATH}");
                logEntry.AppendLine($"[{GetTimestamp()}]   AD 網域: {AD_DOMAIN}");
                logEntry.AppendLine($"[{GetTimestamp()}]   登入帳號: {account}");
                logEntry.AppendLine($"[{GetTimestamp()}]   來源 IP: {GetClientIp()}");

                lock (_logLock)
                {
                    File.AppendAllText(filePath, logEntry.ToString());
                }
            }
            catch { }
        }

        /// <summary>
        /// 寫入詳細的例外 LOG（包含 LDAP 錯誤代碼、HResult、堆疊追蹤等）
        /// </summary>
        private void WriteAdExceptionLog(string logType, string message, Exception ex, string? account = null)
        {
            try
            {
                if (!Directory.Exists(AD_LOG_FOLDER))
                {
                    Directory.CreateDirectory(AD_LOG_FOLDER);
                }

                var fileName = $"AD_Login_{DateTime.Now:yyyy-MM-dd}.txt";
                var filePath = Path.Combine(AD_LOG_FOLDER, fileName);

                var logEntry = new System.Text.StringBuilder();

                // 錯誤標題
                logEntry.AppendLine($"[{GetTimestamp()}] ✗ {message}");
                logEntry.AppendLine($"[{GetTimestamp()}] ========== 登入失敗 ==========");
                logEntry.AppendLine($"[{GetTimestamp()}]   錯誤類型: {ex.GetType().Name}");
                logEntry.AppendLine($"[{GetTimestamp()}]   錯誤訊息: {ex.Message}");

                // HResult 和 LDAP 錯誤代碼
                var ldapErrorCode = GetLdapErrorCode(ex.HResult);
                logEntry.AppendLine($"[{GetTimestamp()}]   錯誤代碼: {ldapErrorCode}");
                logEntry.AppendLine($"[{GetTimestamp()}]   HResult: 0x{ex.HResult:X8}");

                // LDAP 錯誤說明
                var ldapErrorInfo = GetLdapErrorInfo(ex.HResult);
                if (!string.IsNullOrEmpty(ldapErrorInfo))
                {
                    logEntry.AppendLine($"[{GetTimestamp()}]   LDAP 錯誤: {ldapErrorInfo}");
                }

                // COMException 特殊處理
                if (ex is System.Runtime.InteropServices.COMException comEx)
                {
                    logEntry.AppendLine($"[{GetTimestamp()}]   COM ErrorCode: 0x{comEx.ErrorCode:X8}");
                }

                // DirectoryServicesCOMException 特殊處理
                if (ex.GetType().Name == "DirectoryServicesCOMException")
                {
                    var extendedError = ex.GetType().GetProperty("ExtendedError")?.GetValue(ex);
                    var extendedErrorMessage = ex.GetType().GetProperty("ExtendedErrorMessage")?.GetValue(ex);
                    if (extendedError != null)
                        logEntry.AppendLine($"[{GetTimestamp()}]   LDAP ExtendedError: {extendedError}");
                    if (extendedErrorMessage != null)
                        logEntry.AppendLine($"[{GetTimestamp()}]   LDAP ExtendedErrorMessage: {extendedErrorMessage}");
                }

                // 內部例外
                if (ex.InnerException != null)
                {
                    logEntry.AppendLine($"[{GetTimestamp()}]   內部例外類型: {ex.InnerException.GetType().Name}");
                    logEntry.AppendLine($"[{GetTimestamp()}]   內部例外訊息: {ex.InnerException.Message}");
                }

                // 堆疊追蹤
                logEntry.AppendLine($"[{GetTimestamp()}]   堆疊追蹤: {ex.StackTrace}");
                logEntry.AppendLine();

                lock (_logLock)
                {
                    File.AppendAllText(filePath, logEntry.ToString());
                }
            }
            catch { }
        }

        /// <summary>
        /// 寫入登入成功的 LOG
        /// </summary>
        private void WriteAdLoginSuccessLog(string account, string cn, string dept, string mail)
        {
            try
            {
                if (!Directory.Exists(AD_LOG_FOLDER))
                {
                    Directory.CreateDirectory(AD_LOG_FOLDER);
                }

                var fileName = $"AD_Login_{DateTime.Now:yyyy-MM-dd}.txt";
                var filePath = Path.Combine(AD_LOG_FOLDER, fileName);

                var logEntry = new System.Text.StringBuilder();
                logEntry.AppendLine($"[{GetTimestamp()}] ✓ AD 驗證成功");
                logEntry.AppendLine($"[{GetTimestamp()}] ========== 登入成功 ==========");
                logEntry.AppendLine($"[{GetTimestamp()}]   帳號: {account}");
                logEntry.AppendLine($"[{GetTimestamp()}]   姓名: {cn}");
                logEntry.AppendLine($"[{GetTimestamp()}]   部門: {dept}");
                logEntry.AppendLine($"[{GetTimestamp()}]   Email: {mail}");
                logEntry.AppendLine();

                lock (_logLock)
                {
                    File.AppendAllText(filePath, logEntry.ToString());
                }
            }
            catch { }
        }

        /// <summary>
        /// 根據 HResult 取得 LDAP 錯誤代碼數字
        /// </summary>
        private static int GetLdapErrorCode(int hResult)
        {
            // 從 HResult 提取 LDAP 錯誤代碼
            // LDAP 錯誤代碼通常在低位元組
            return hResult switch
            {
                unchecked((int)0x8007203A) => 81,  // LDAP_SERVER_DOWN
                unchecked((int)0x80072020) => 1,   // LDAP_OPERATIONS_ERROR
                unchecked((int)0x80072021) => 2,   // LDAP_PROTOCOL_ERROR
                unchecked((int)0x80072022) => 3,   // LDAP_TIMELIMIT_EXCEEDED
                unchecked((int)0x80072023) => 4,   // LDAP_SIZELIMIT_EXCEEDED
                unchecked((int)0x8007052E) => 49,  // LDAP_INVALID_CREDENTIALS
                unchecked((int)0x80070525) => 49,  // USER_NOT_FOUND (mapped to invalid credentials)
                unchecked((int)0x80070533) => 53,  // ACCOUNT_DISABLED
                _ => hResult & 0xFFFF
            };
        }

        /// <summary>
        /// 根據 HResult 取得 LDAP 錯誤說明
        /// </summary>
        private static string GetLdapErrorInfo(int hResult)
        {
            return hResult switch
            {
                // 網路/連線錯誤
                unchecked((int)0x8007203A) => "LDAP_SERVER_DOWN (81) - AD 伺服器無法連線或已關閉",
                unchecked((int)0x80072020) => "LDAP_OPERATIONS_ERROR (1) - 操作錯誤",
                unchecked((int)0x80072021) => "LDAP_PROTOCOL_ERROR (2) - 協定錯誤",
                unchecked((int)0x80072022) => "LDAP_TIMELIMIT_EXCEEDED (3) - 連線逾時",
                unchecked((int)0x80072023) => "LDAP_SIZELIMIT_EXCEEDED (4) - 結果數量超過限制",
                unchecked((int)0x80072027) => "LDAP_AUTH_METHOD_NOT_SUPPORTED (7) - 不支援的驗證方法",
                unchecked((int)0x80072028) => "LDAP_STRONG_AUTH_REQUIRED (8) - 需要強式驗證",
                unchecked((int)0x80072035) => "LDAP_NO_SUCH_OBJECT (32) - 找不到物件",
                unchecked((int)0x80072037) => "LDAP_INVALID_DN_SYNTAX (34) - 無效的 DN 語法",

                // 驗證錯誤
                unchecked((int)0x8007052E) => "LDAP_INVALID_CREDENTIALS (49) - 帳號或密碼錯誤",
                unchecked((int)0x80070525) => "USER_NOT_FOUND - 找不到使用者",
                unchecked((int)0x80070533) => "ACCOUNT_DISABLED (53) - 帳號已停用",
                unchecked((int)0x80070701) => "ACCOUNT_EXPIRED - 帳號已過期",
                unchecked((int)0x80070532) => "PASSWORD_EXPIRED - 密碼已過期",
                unchecked((int)0x80070775) => "ACCOUNT_LOCKED_OUT - 帳號已被鎖定",
                unchecked((int)0x8007052F) => "INVALID_LOGON_HOURS - 不在允許的登入時段",
                unchecked((int)0x80070530) => "INVALID_WORKSTATION - 不允許從此工作站登入",
                unchecked((int)0x80070773) => "PASSWORD_MUST_CHANGE - 必須變更密碼",

                // 其他常見錯誤
                unchecked((int)0x80004005) => "E_FAIL - 一般性失敗",
                unchecked((int)0x80070005) => "E_ACCESSDENIED - 存取被拒",
                unchecked((int)0x80072EE2) => "WININET_TIMEOUT - 連線逾時",
                unchecked((int)0x80072EFD) => "WININET_CANNOT_CONNECT - 無法建立連線",

                _ => $"未知錯誤代碼: 0x{hResult:X8}"
            };
        }

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

            if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) device = "Windows";
            else if (userAgent.Contains("Mac", StringComparison.OrdinalIgnoreCase)) device = "macOS";
            else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase)) device = "Linux";
            else if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) device = "iPhone";
            else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)) device = "Android";

            if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase) && !userAgent.Contains("Chromium")) browser = "Chrome";
            else if (userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase) && !userAgent.Contains("Chrome")) browser = "Safari";
            else if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) browser = "Firefox";
            else if (userAgent.Contains("Edge", StringComparison.OrdinalIgnoreCase)) browser = "Edge";

            return (browser, device);
        }

        // ===================== AD 驗證 =====================
        private bool LdapLogin(string account, string password, out string cn, out string dept, out string mail)
        {
            cn = dept = mail = "";
            var clientIp = GetClientIp();

            // ========== 開始 AD 驗證 ==========
            WriteAdLoginStartLog(account);
            _ = service.LogServices.LogAsync("ad_login_start", JsonConvert.SerializeObject(new
            {
                Account = account,
                AdPath = AD_PATH,
                AdDomain = AD_DOMAIN,
                ClientIp = clientIp,
                Timestamp = DateTime.Now
            }));

            try
            {
                WriteAdLog("AD_CONNECT", "正在連接 AD 伺服器...", account);

                using var entry = new DirectoryEntry(AD_PATH, $"{AD_DOMAIN}\\{account}", password);

                WriteAdLog("AD_AUTH", "正在驗證帳號密碼...", account);
                object nativeObj = entry.NativeObject; // 密碼錯誤這裡會丟例外

                WriteAdLog("AD_CONNECT_SUCCESS", "AD 連線驗證成功", account);
                _ = service.LogServices.LogAsync("ad_connect_success", JsonConvert.SerializeObject(new
                {
                    Account = account,
                    Message = "AD 連線驗證成功",
                    Timestamp = DateTime.Now
                }));

                WriteAdLog("AD_SEARCH", $"正在搜尋使用者資訊 (Filter: sAMAccountName={account})...", account);
                using var searcher = new DirectorySearcher(entry);
                searcher.Filter = $"(sAMAccountName={account})";
                searcher.PropertiesToLoad.Add("cn");
                searcher.PropertiesToLoad.Add("department");
                searcher.PropertiesToLoad.Add("mail");

                var result = searcher.FindOne();
                if (result == null)
                {
                    WriteAdLog("AD_SEARCH_FAIL", "✗ 搜尋不到使用者資訊", account);
                    _ = service.LogServices.LogAsync("ad_search_not_found", JsonConvert.SerializeObject(new
                    {
                        Account = account,
                        Message = "AD 驗證成功但搜尋不到使用者資訊",
                        Timestamp = DateTime.Now
                    }));
                    return false;
                }

                cn = result.Properties["cn"][0]?.ToString() ?? account;
                if (result.Properties.Contains("department"))
                    dept = result.Properties["department"][0]?.ToString() ?? "";
                if (result.Properties.Contains("mail"))
                    mail = result.Properties["mail"][0]?.ToString() ?? "";

                // 寫入成功 LOG
                WriteAdLoginSuccessLog(account, cn, dept, mail);
                _ = service.LogServices.LogAsync("ad_login_success", JsonConvert.SerializeObject(new
                {
                    Account = account,
                    Name = cn,
                    Department = dept,
                    Email = mail,
                    ClientIp = clientIp,
                    Timestamp = DateTime.Now
                }));

                return true;
            }
            catch (Exception ex)
            {
                // ========== AD 驗證失敗（詳細記錄） ==========
                WriteAdExceptionLog("AD_LOGIN_FAILED", "AD 驗證失敗", ex, account);

                // 同時寫入資料庫 LOG
                _ = service.LogServices.LogAsync("ad_login_failed", JsonConvert.SerializeObject(new
                {
                    Account = account,
                    ErrorMessage = ex.Message,
                    ErrorType = ex.GetType().FullName,
                    HResult = $"0x{ex.HResult:X8}",
                    LdapError = GetLdapErrorInfo(ex.HResult),
                    InnerException = ex.InnerException?.Message,
                    ClientIp = clientIp,
                    Timestamp = DateTime.Now
                }));
                return false;
            }
        }

        // ===================== 自動建立 AD 使用者 =====================
        private AuthUser CreateUserFromAD(string account, string password, string cn, string mail)
        {
            var newUserId = Guid.NewGuid();
            var clientIp = GetClientIp();

            // ========== STEP 1: 開始建立使用者 ==========
            WriteAdLog("AD_USER_CREATE_START", $"開始建立 AD 使用者，UserId: {newUserId}", account);
            _ = service.LogServices.LogAsync("ad_user_start", JsonConvert.SerializeObject(new
            {
                Account = account,
                Name = cn,
                Email = mail,
                NewUserId = newUserId,
                Timestamp = DateTime.Now
            }));

            try
            {
                // ========== STEP 2: 雜湊密碼 ==========
                WriteAdLog("AD_USER_HASH", "正在雜湊密碼...", account);
                var hashResult = HashString.Hash(password);

                // ========== STEP 3: 查詢預設角色 ==========
                WriteAdLog("AD_USER_ROLE", $"正在查詢預設角色，RoleId: {DEFAULT_ROLE_ID}", account);
                var role = db.AuthRole.FirstOrDefault(x => x.Id == DEFAULT_ROLE_ID);
                if (role == null)
                {
                    WriteAdLog("AD_USER_ROLE_FAIL", $"找不到預設角色！RoleId: {DEFAULT_ROLE_ID}", account);
                    _ = service.LogServices.LogAsync("ad_user_fail", JsonConvert.SerializeObject(new
                    {
                        Account = account,
                        ErrorMessage = "找不到預設角色",
                        DefaultRoleId = DEFAULT_ROLE_ID,
                        Timestamp = DateTime.Now
                    }));
                    throw new HttpException("找不到預設角色");
                }
                WriteAdLog("AD_USER_ROLE_OK", $"找到預設角色: {role.Name}", account);

                // ========== STEP 4: 建立使用者物件 ==========
                WriteAdLog("AD_USER_BUILD", "正在建立使用者物件...", account);
                var newUser = new AuthUser
                {
                    Id = newUserId,
                    Name = string.IsNullOrEmpty(cn) ? account : cn,
                    Account = account,
                    PasswordHash = hashResult.Hash,
                    PasswordSalt = hashResult.Salt,
                    Email = string.IsNullOrEmpty(mail) ? null : mail,
                    DepartmentId = DEFAULT_DEPARTMENT_ID,
                    IsEnabled = true,
                    IsApproved = true,
                    CreateAt = DateTime.Now,
                    CreateBy = SYSTEM_USER_ID,
                };

                newUser.AuthRole.Add(role);

                // ========== STEP 5: 儲存到資料庫 ==========
                WriteAdLog("AD_USER_SAVE", "正在儲存使用者到資料庫...", account);
                db.AuthUser.Add(newUser);
                db.SaveChanges();

                // ========== STEP 6: 建立成功 ==========
                WriteAdLog("AD_USER_SUCCESS", $"AD 使用者建立成功！姓名: {newUser.Name}, Email: {newUser.Email}, 角色: {role.Name}", account);
                _ = service.LogServices.LogAsync("ad_user_created", JsonConvert.SerializeObject(new
                {
                    Account = account,
                    UserId = newUserId,
                    Name = newUser.Name,
                    Email = newUser.Email,
                    DepartmentId = DEFAULT_DEPARTMENT_ID,
                    RoleId = DEFAULT_ROLE_ID,
                    RoleName = role.Name,
                    ClientIp = clientIp,
                    Timestamp = DateTime.Now
                }));

                return db.AuthUser
                    .Include(x => x.AuthRole)
                    .Include(x => x.Department)
                    .AsNoTracking()
                    .WhereNotDeleted()
                    .First(x => x.Account == account);
            }
            catch (Exception ex) when (ex is not HttpException)
            {
                // ========== 建立失敗（詳細記錄） ==========
                WriteAdExceptionLog("AD_USER_FAILED", "AD 使用者建立失敗", ex, account);

                _ = service.LogServices.LogAsync("ad_user_fail", JsonConvert.SerializeObject(new
                {
                    Account = account,
                    ErrorMessage = ex.Message,
                    ErrorType = ex.GetType().FullName,
                    HResult = $"0x{ex.HResult:X8}",
                    InnerException = ex.InnerException?.Message,
                    StackTrace = ex.StackTrace,
                    Timestamp = DateTime.Now
                }));
                throw;
            }
        }

        // ===================== 驗證使用者 =====================
        public AuthUser? IsValidUser(string account, string password)
        {
            // 1. 先查 DB
            var user = db.AuthUser
                .Include(x => x.AuthRole)
                .Include(x => x.Department)
                .AsNoTracking()
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Account == account);

            // 2. DB 有找到 → 直接本地驗證
            if (user != null)
            {
                if (!HashString.Verify(password, user.PasswordHash, user.PasswordSalt))
                    return null;

                IsEnabled(user);
                return user;
            }

            // ========== 本地帳號不存在，嘗試 AD 驗證 ==========
            WriteAdLog("AD_AUTH_ATTEMPT", "本地帳號不存在，準備嘗試 AD 驗證", account);
            _ = service.LogServices.LogAsync("ad_auth_attempt", JsonConvert.SerializeObject(new
            {
                Account = account,
                Message = "本地帳號不存在，嘗試 AD 驗證",
                ClientIp = GetClientIp(),
                Timestamp = DateTime.Now
            }));

            // 3. DB 沒找到 → 去 AD 驗證
            var adSuccess = LdapLogin(account, password, out string cn, out string dept, out string mail);
            if (!adSuccess)
            {
                WriteAdLog("AD_AUTH_RESULT", "AD 驗證失敗，登入流程結束", account);
                return null;
            }

            // 4. AD 驗證成功 → 自動建立並回傳
            WriteAdLog("AD_AUTH_RESULT", "AD 驗證成功，準備建立本地帳號", account);
            return CreateUserFromAD(account, password, cn, mail);
        }

        // ===================== 登入 =====================
        public AuthUser Login(LoginVM vm)
        {
            var user = IsValidUser(vm.Account, vm.Password);
            if (user == null)
            {
                var deviceInfo = GetDeviceInfo();
                var failureInfo = new
                {
                    UserName = vm.Account,
                    IsSuccess = false,
                    FailureReason = "帳號或密碼錯誤",
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

            var successDeviceInfo = GetDeviceInfo();
            var successInfo = new
            {
                UserName = user.Account,
                IsSuccess = true,
                ClientIp = GetClientIp(),
                DeviceInfo = successDeviceInfo.device,
                BrowserInfo = successDeviceInfo.browser,
                Timestamp = DateTime.Now
            };
            _ = service.LogServices.LogAsync("login_success", JsonConvert.SerializeObject(successInfo), user.Id, user.DepartmentId);
            return user;
        }

        // ===================== 以下保持原樣 =====================
        public void IsEnabled(AuthUser? user)
        {
            if (user?.IsEnabled == false)
            {
                var reason = user.IsApproved ? "帳號已停用" : "帳號尚在審核中，請等待管理員核准";
                var failureReason = user.IsApproved ? "帳號已停用" : "帳號尚在審核中";
                var deviceInfo = GetDeviceInfo();
                var failureInfo = new
                {
                    UserName = user.Account,
                    IsSuccess = false,
                    FailureReason = failureReason,
                    ClientIp = GetClientIp(),
                    DeviceInfo = deviceInfo.device,
                    BrowserInfo = deviceInfo.browser,
                    Timestamp = DateTime.Now
                };
                _ = service.LogServices.LogAsync("login_failed", JsonConvert.SerializeObject(failureInfo), user.Id, user.DepartmentId);
                throw new HttpException(reason)
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

        public void GenerateCookie(IResponseCookies cookies, AuthUser user)
        {
            var isRoomManager = db.SysRoom
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Any(r => r.ManagerId == user.Id && r.IsEnabled && r.DeleteAt == null);

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
            return authUser != null ? "/Auth/Profiles" : "/";
        }
    }
}
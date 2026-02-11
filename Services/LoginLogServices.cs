using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TASA.Models;
using TASA.Program;

namespace TASA.Services
{
    public class LoginLogServices(TASAContext db) : IService
    {
        // ✅ QueryVM 改成繼承 BaseQueryVM
        public class QueryVM : BaseQueryVM
        {
            public string StartDate { get; set; } = string.Empty;
            public string EndDate { get; set; } = string.Empty;
            public string InfoType { get; set; } = "all";
        }

        public class ListVM
        {
            public uint Id { get; set; }
            public DateTime LoginTime { get; set; }
            public string UserName { get; set; } = string.Empty;
            public string LoginMethod { get; set; } = string.Empty;
            public bool IsSuccess { get; set; }
            public string? FailureReason { get; set; }
            public string IpAddress { get; set; } = string.Empty;
            public string DeviceInfo { get; set; } = string.Empty;
            public string BrowserInfo { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string ActionDescription { get; set; } = string.Empty;  // 操作描述
        }

        public IQueryable<ListVM> List(QueryVM query)
        {
            // ✅ 驗證日期參數
            if (string.IsNullOrEmpty(query.StartDate) || string.IsNullOrEmpty(query.EndDate))
            {
                return new List<ListVM>().AsQueryable();
            }

            // ✅ 解析日期
            var startDate = DateTime.Parse(query.StartDate);
            var endDate = DateTime.Parse(query.EndDate).AddDays(1);

            // ✅ 從資料庫查詢日期範圍內的記錄
            var logs = db.LogSys
                .AsNoTracking()
                .Where(x => x.Time >= startDate && x.Time < endDate)
                .OrderByDescending(x => x.Time)
                .ToList();

            // ✅ 根據 InfoType 過濾（在記憶體中進行）
            if (!string.IsNullOrEmpty(query.InfoType) && query.InfoType != "all")
            {
                logs = logs.Where(x => x.InfoType.StartsWith(query.InfoType)).ToList();
            }

            // ✅ 將記錄轉換為前端需要的格式
            var result = logs
                .Select(x =>
                {
                    try
                    {
                        var info = JsonConvert.DeserializeObject<dynamic>(x.Info);
                        var type = ExtractType(x.InfoType);

                        return new ListVM
                        {
                            Id = x.No,
                            LoginTime = x.Time,
                            UserName = info?.UserName ?? "未知",
                            LoginMethod = info?.LoginMethod ?? "未知",
                            IsSuccess = info?.IsSuccess ?? false,
                            FailureReason = info?.FailureReason,
                            IpAddress = info?.ClientIp ?? info?.IpAddress ?? x.Ip ?? "未知",
                            DeviceInfo = info?.DeviceInfo ?? "Unknown",
                            BrowserInfo = info?.BrowserInfo ?? "Unknown",
                            Type = type,
                            ActionDescription = GenerateActionDescription(x.InfoType, info)
                        };
                    }
                    catch
                    {
                        return new ListVM
                        {
                            Id = x.No,
                            LoginTime = x.Time,
                            UserName = x.Info.Length > 50 ? x.Info.Substring(0, 50) + "..." : x.Info,
                            LoginMethod = "未知",
                            IsSuccess = x.InfoType.Contains("success"),
                            FailureReason = null,
                            IpAddress = x.Ip ?? "未知",
                            DeviceInfo = "Unknown",
                            BrowserInfo = "Unknown",
                            Type = ExtractType(x.InfoType),
                            ActionDescription = x.Info.Length > 50 ? x.Info.Substring(0, 50) + "..." : x.Info
                        };
                    }
                })
                .ToList();

            // ✅ 關鍵字過濾（在記憶體中進行）
            if (!string.IsNullOrEmpty(query.Keyword))
            {
                result = result
                    .Where(x =>
                        x.UserName.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) ||
                        x.IpAddress.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) ||
                        x.DeviceInfo.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            }


            return result.AsQueryable();
        }

        /// <summary>
        /// 根據 InfoType 提取簡化的類型
        /// 用於前端 Tab 的過濾
        /// </summary>
        private string ExtractType(string infoType)
        {
            return infoType switch
            {
                var x when x.Contains("login") => "login",
                var x when x.Contains("user_register") => "user_register",
                var x when x.Contains("user_update") => "user_update",  // 包含 user_update_profile, user_update_password
                var x when x.Contains("user_insert") || x.Contains("user_create") => "user_insert",
                var x when x.Contains("user_delete") => "user_delete",
                var x when x.Contains("變更密碼") || x.Contains("忘記密碼") => "user_update",  // 相容舊資料
                _ => "other"
            };
        }

        /// <summary>
        /// 根據 InfoType 和 Info 生成操作描述
        /// 格式: "操作者 修改了 目標 操作內容"
        /// </summary>
        private string GenerateActionDescription(string infoType, dynamic? info)
        {
            if (info == null) return "-";

            string operatorName = info?.OperatorName ?? info?.UserName ?? "系統";
            string targetName = info?.TargetName ?? info?.UserName ?? "";
            string action = info?.Action?.ToString() ?? "";

            // 根據 InfoType 生成描述
            if (infoType.Contains("user_update_password") || infoType.Contains("變更密碼"))
            {
                if (action == "reset_password")
                    return $"{targetName} 透過忘記密碼重設了密碼";
                return $"{operatorName} 修改了 {targetName} 的密碼";
            }

            if (infoType.Contains("user_update_profile"))
            {
                var changes = new List<string>();
                if (info?.OldName != null && info?.NewName != null && info.OldName.ToString() != info.NewName.ToString())
                    changes.Add($"名稱: {info.OldName} → {info.NewName}");
                if (info?.OldEmail != null && info?.NewEmail != null && info.OldEmail.ToString() != info.NewEmail.ToString())
                    changes.Add($"Email: {info.OldEmail} → {info.NewEmail}");

                var changeDesc = changes.Count > 0 ? string.Join(", ", changes) : "個人資料";
                return $"{operatorName} 修改了 {targetName} 的{changeDesc}";
            }

            if (infoType.Contains("user_update") || infoType == "user_update")
            {
                // 檢查是否有 IsEnabled 變更
                if (info?.IsEnabled != null)
                {
                    bool isEnabled = info.IsEnabled;
                    return isEnabled
                        ? $"{operatorName} 啟用了 {targetName} 的帳號"
                        : $"{operatorName} 停用了 {targetName} 的帳號";
                }
                return $"{operatorName} 修改了 {targetName} 的帳號資料";
            }

            if (infoType.Contains("user_insert") || infoType.Contains("user_create"))
            {
                return $"{operatorName} 新增了帳號 {targetName}";
            }

            if (infoType.Contains("user_delete"))
            {
                return $"{operatorName} 刪除了帳號 {targetName}";
            }

            // 預設描述
            return $"{operatorName} 執行了 {infoType} 操作";
        }
    }
}
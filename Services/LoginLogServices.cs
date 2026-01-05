using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TASA.Models;
using TASA.Program;

namespace TASA.Services
{
    public class LoginLogServices(TASAContext db) : IService
    {
        public record QueryVM
        {
            public string Keyword { get; set; } = string.Empty;
            public string StartDate { get; set; } = string.Empty;
            public string EndDate { get; set; } = string.Empty;
            public string InfoType { get; set; } = "all";
        }

        public record ListVM
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
        }

        public List<ListVM> List(QueryVM query)
        {
            // ✅ 驗證日期參數
            if (string.IsNullOrEmpty(query.StartDate) || string.IsNullOrEmpty(query.EndDate))
            {
                return new List<ListVM>();
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
                        
                        return new ListVM
                        {
                            Id = x.No,
                            LoginTime = x.Time,
                            UserName = info?.UserName ?? "未知",
                            LoginMethod = info?.LoginMethod ?? "未知",
                            IsSuccess = info?.IsSuccess ?? false,
                            FailureReason = info?.FailureReason,
                            IpAddress = x.Ip,
                            DeviceInfo = info?.DeviceInfo ?? "Unknown",
                            BrowserInfo = info?.BrowserInfo ?? "Unknown",
                            Type = ExtractType(x.InfoType)
                        };
                    }
                    catch
                    {
                        // ✅ 異常處理 - 如果 JSON 解析失敗
                        return new ListVM
                        {
                            Id = x.No,
                            LoginTime = x.Time,
                            UserName = x.Info.Length > 50 ? x.Info.Substring(0, 50) + "..." : x.Info,
                            LoginMethod = "未知",
                            IsSuccess = x.InfoType.Contains("success"),
                            FailureReason = null,
                            IpAddress = x.Ip,
                            DeviceInfo = "Unknown",
                            BrowserInfo = "Unknown",
                            Type = ExtractType(x.InfoType)
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

            return result;
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
                var x when x.Contains("user_update") => "user_update",
                var x when x.Contains("user_insert") || x.Contains("user_create") => "user_insert",
                var x when x.Contains("user_delete") => "user_delete",
                _ => "other"
            };
        }
    }
}
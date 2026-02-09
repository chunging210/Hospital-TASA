using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services
{
    public class SysConfigService(TASAContext db, ServiceWrapper service) : IService
    {

        public class ToggleRequest
        {
            public bool IsOpen { get; set; }
        }

        public class UpdateSysConfigDto
        {
            public string ConfigKey { get; set; }
            public string ConfigValue { get; set; }
            public Guid? DepartmentId { get; set; }  // NULL = 全局預設
        }

        // 程式內建預設值
        private static readonly Dictionary<string, string> ProgramDefaults = new()
        {
            ["GUEST_REGISTRATION"] = "true",
            ["PAYMENT_DEADLINE_DAYS"] = "7",
            ["MIN_ADVANCE_BOOKING_DAYS"] = "14",
            ["MANAGER_EMAIL"] = ""
        };

        // ========== 統一的 Config 處理方法 ==========

        /// <summary>
        /// 取得設定值 (優先順序: 分院設定 > 全局設定 > 程式預設)
        /// </summary>
        private string GetConfigValue(string configKey, Guid? departmentId = null, string? programDefault = null)
        {
            // 1. 如果有指定分院，先找分院設定
            if (departmentId.HasValue)
            {
                var departmentConfig = db.SysConfig
                    .AsNoTracking()
                    .FirstOrDefault(x =>
                        x.ConfigKey == configKey &&
                        x.DepartmentId == departmentId.Value &&
                        x.DeleteAt == null &&
                        x.Enabled);

                if (departmentConfig != null)
                {
                    return departmentConfig.ConfigValue;
                }
            }

            // 2. 找全局設定 (DepartmentId = NULL)
            var globalConfig = db.SysConfig
                .AsNoTracking()
                .FirstOrDefault(x =>
                    x.ConfigKey == configKey &&
                    x.DepartmentId == null &&
                    x.DeleteAt == null &&
                    x.Enabled);

            if (globalConfig != null)
            {
                return globalConfig.ConfigValue;
            }

            // 3. 回傳程式預設值
            if (programDefault != null)
            {
                return programDefault;
            }

            return ProgramDefaults.TryGetValue(configKey, out var defaultValue) ? defaultValue : "";
        }

        /// <summary>
        /// 設定值 (通用方法)
        /// </summary>
        private void SetConfigValue(string configKey, string configValue, Guid? departmentId = null)
        {
            // ✅ 修正 NULL 比較問題
            var config = db.SysConfig
                .FirstOrDefault(x =>
                    x.ConfigKey == configKey &&
                    (departmentId == null ? x.DepartmentId == null : x.DepartmentId == departmentId) &&
                    x.DeleteAt == null);

            var departmentName = departmentId.HasValue
                ? db.SysDepartment.FirstOrDefault(d => d.Id == departmentId.Value)?.Name ?? "未知分院"
                : "全局";

            if (config == null)
            {
                // 新增
                config = new SysConfig
                {
                    Id = Guid.NewGuid(),
                    ConfigKey = configKey,
                    ConfigValue = configValue,
                    DepartmentId = departmentId,
                    Enabled = true
                };
                db.SysConfig.Add(config);
                db.SaveChanges();
                _ = service.LogServices.LogAsync("系統設定新增", $"[{departmentName}] {configKey}: {configValue}");
            }
            else
            {
                // 更新
                var oldValue = config.ConfigValue;
                config.ConfigValue = configValue;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("系統設定編輯", $"[{departmentName}] {configKey}: {oldValue} -> {configValue}");
            }
        }

        // ========== 取得所有系統設定 ==========

        /// <summary>
        /// 取得所有系統設定（根據當前使用者的分院）
        /// </summary>
        public Dictionary<string, string> GetAllConfigs()
        {
            var currentUser = service.UserClaimsService.Me();
            var departmentId = currentUser?.DepartmentId;

            return GetAllConfigs(departmentId);
        }

        /// <summary>
        /// 取得指定分院的所有設定（合併全局預設）
        /// </summary>
        public Dictionary<string, string> GetAllConfigs(Guid? departmentId)
        {
            var result = new Dictionary<string, string>();

            // 1. 先放程式預設值
            foreach (var (key, value) in ProgramDefaults)
            {
                result[key] = value;
            }

            // 2. 覆蓋全局設定
            var globalConfigs = db.SysConfig
                .AsNoTracking()
                .Where(x => x.DeleteAt == null && x.Enabled && x.DepartmentId == null)
                .ToList();

            foreach (var config in globalConfigs)
            {
                result[config.ConfigKey] = config.ConfigValue;
            }

            // 3. 如果有指定分院，覆蓋分院設定
            if (departmentId.HasValue)
            {
                var departmentConfigs = db.SysConfig
                    .AsNoTracking()
                    .Where(x => x.DeleteAt == null && x.Enabled && x.DepartmentId == departmentId.Value)
                    .ToList();

                foreach (var config in departmentConfigs)
                {
                    result[config.ConfigKey] = config.ConfigValue;
                }
            }

            return result;
        }

        /// <summary>
        /// 取得全局設定（Admin 用）
        /// </summary>
        public Dictionary<string, string> GetGlobalConfigs()
        {
            return GetAllConfigs(null);
        }

        /// <summary>
        /// 更新設定 (通用方法)
        /// </summary>
        public void UpdateConfig(string configKey, string configValue, Guid? departmentId = null)
        {
            // ✅ 驗證：最早預約天數必須大於繳費期限天數
            if (configKey == "PAYMENT_DEADLINE_DAYS" || configKey == "MIN_ADVANCE_BOOKING_DAYS")
            {
                ValidateBookingAndPaymentDays(configKey, configValue, departmentId);
            }

            SetConfigValue(configKey, configValue, departmentId);
        }

        /// <summary>
        /// 驗證最早預約天數與繳費期限天數的關係
        /// </summary>
        private void ValidateBookingAndPaymentDays(string configKey, string newValue, Guid? departmentId)
        {
            if (!int.TryParse(newValue, out int newDays))
            {
                throw new HttpException("請輸入有效的數字");
            }

            int minAdvanceBookingDays;
            int paymentDeadlineDays;

            if (configKey == "MIN_ADVANCE_BOOKING_DAYS")
            {
                // 正在修改最早預約天數，取得目前的繳費期限天數
                minAdvanceBookingDays = newDays;
                paymentDeadlineDays = GetPaymentDeadlineDays(departmentId);
            }
            else
            {
                // 正在修改繳費期限天數，取得目前的最早預約天數
                paymentDeadlineDays = newDays;
                minAdvanceBookingDays = GetMinAdvanceBookingDays(departmentId);
            }

            // 最早預約天數必須大於繳費期限天數
            if (minAdvanceBookingDays <= paymentDeadlineDays)
            {
                throw new HttpException(
                    $"最早預約天數（{minAdvanceBookingDays} 天）必須大於繳費期限天數（{paymentDeadlineDays} 天），" +
                    $"否則可能發生會議已開始但尚未繳費的情況");
            }
        }

        /// <summary>
        /// 刪除分院設定（恢復使用全局預設）
        /// </summary>
        public void DeleteDepartmentConfig(string configKey, Guid departmentId)
        {
            var config = db.SysConfig
                .FirstOrDefault(x =>
                    x.ConfigKey == configKey &&
                    x.DepartmentId == departmentId &&
                    x.DeleteAt == null);

            if (config != null)
            {
                config.DeleteAt = DateTime.Now;
                db.SaveChanges();

                var departmentName = db.SysDepartment.FirstOrDefault(d => d.Id == departmentId)?.Name ?? "未知分院";
                _ = service.LogServices.LogAsync("系統設定刪除", $"[{departmentName}] {configKey} (恢復使用全局預設)");
            }
        }

        // ========== 1. 是否開放註冊 ==========

        /// <summary>
        /// 取得「是否開放註冊」
        /// </summary>
        public bool IsRegistrationOpen(Guid? departmentId = null)
        {
            var value = GetConfigValue("GUEST_REGISTRATION", departmentId);
            return value.ToLower() == "true";
        }

        /// <summary>
        /// 設定「是否開放註冊」
        /// </summary>
        public void SetRegistrationOpen(bool isOpen, Guid? departmentId = null)
        {
            SetConfigValue("GUEST_REGISTRATION", isOpen.ToString().ToLower(), departmentId);
        }

        // ========== 2. 繳費期限天數 ==========

        /// <summary>
        /// 取得繳費期限天數
        /// </summary>
        public int GetPaymentDeadlineDays(Guid? departmentId = null)
        {
            var value = GetConfigValue("PAYMENT_DEADLINE_DAYS", departmentId);
            return int.TryParse(value, out int days) ? days : 7;
        }

        /// <summary>
        /// 設定繳費期限天數
        /// </summary>
        public void SetPaymentDeadlineDays(int days, Guid? departmentId = null)
        {
            if (days < 1) days = 1;
            if (days > 30) days = 30;
            SetConfigValue("PAYMENT_DEADLINE_DAYS", days.ToString(), departmentId);
        }

        // ========== 3. 最早預約天數 ==========

        /// <summary>
        /// 取得最早預約天數
        /// </summary>
        public int GetMinAdvanceBookingDays(Guid? departmentId = null)
        {
            var value = GetConfigValue("MIN_ADVANCE_BOOKING_DAYS", departmentId);
            return int.TryParse(value, out int days) ? days : 14;
        }

        /// <summary>
        /// 設定最早預約天數
        /// </summary>
        public void SetMinAdvanceBookingDays(int days, Guid? departmentId = null)
        {
            if (days < 0) days = 0;
            if (days > 365) days = 365;
            SetConfigValue("MIN_ADVANCE_BOOKING_DAYS", days.ToString(), departmentId);
        }

        // ========== 4. 主管信箱 ==========

        /// <summary>
        /// 取得主管信箱
        /// </summary>
        public string GetManagerEmail(Guid? departmentId = null)
        {
            return GetConfigValue("MANAGER_EMAIL", departmentId);
        }

        /// <summary>
        /// 設定主管信箱
        /// </summary>
        public void SetManagerEmail(string email, Guid? departmentId = null)
        {
            SetConfigValue("MANAGER_EMAIL", email ?? "", departmentId);
        }

    }
}

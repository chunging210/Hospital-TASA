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
        }


        // ========== 統一的 Config 處理方法 ==========

        /// <summary>
        /// 取得設定值 (通用方法)
        /// </summary>
        private string GetConfigValue(string configKey, string defaultValue = "")
        {
            var config = db.SysConfig
                .AsNoTracking()
                .FirstOrDefault(x =>
                    x.ConfigKey == configKey &&
                    x.DeleteAt == null);

            return config?.ConfigValue ?? defaultValue;
        }

        /// <summary>
        /// 設定值 (通用方法)
        /// </summary>
        private void SetConfigValue(string configKey, string configValue)
        {
            var config = db.SysConfig
                .FirstOrDefault(x =>
                    x.ConfigKey == configKey &&
                    x.DeleteAt == null);

            if (config == null)
            {
                // 新增
                config = new SysConfig
                {
                    Id = Guid.NewGuid(),
                    ConfigKey = configKey,
                    ConfigValue = configValue,
                    Enabled = true
                };
                db.SysConfig.Add(config);
                db.SaveChanges();
                _ = service.LogServices.LogAsync("系統設定新增", $"{configKey}: {configValue}");
            }
            else
            {
                // 更新
                var oldValue = config.ConfigValue;
                config.ConfigValue = configValue;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("系統設定編輯", $"{configKey}: {oldValue} -> {configValue}");
            }
        }

        // ========== 取得所有系統設定 ==========

        /// <summary>
        /// 取得所有系統設定
        /// </summary>
        public Dictionary<string, string> GetAllConfigs()
        {
            var configs = db.SysConfig
                .AsNoTracking()
                .Where(x => x.DeleteAt == null && x.Enabled)
                .ToDictionary(x => x.ConfigKey, x => x.ConfigValue);

            // 確保所有設定都有預設值
            var defaults = new Dictionary<string, string>
            {
                ["GUEST_REGISTRATION"] = "true",
                ["PAYMENT_DEADLINE_DAYS"] = "7",
                ["MIN_ADVANCE_BOOKING_DAYS"] = "7",
                ["MANAGER_EMAIL"] = "",
                ["AUTO_RELEASE_AFTER_DEADLINE"] = "true"
            };

            foreach (var (key, defaultValue) in defaults)
            {
                if (!configs.ContainsKey(key))
                {
                    configs[key] = defaultValue;
                }
            }

            return configs;
        }

        /// <summary>
        /// 更新設定 (通用方法)
        /// </summary>
        public void UpdateConfig(string configKey, string configValue)
        {
            SetConfigValue(configKey, configValue);
        }

        // ========== 1. 是否開放註冊 ==========

        /// <summary>
        /// 取得「是否開放註冊」
        /// </summary>
        public bool IsRegistrationOpen()
        {
            var value = GetConfigValue("GUEST_REGISTRATION", "true");
            return value.ToLower() == "true";
        }

        /// <summary>
        /// 設定「是否開放註冊」
        /// </summary>
        public void SetRegistrationOpen(bool isOpen)
        {
            SetConfigValue("GUEST_REGISTRATION", isOpen.ToString().ToLower());
        }

        // ========== 2. 繳費期限天數 ==========

        /// <summary>
        /// 取得繳費期限天數
        /// </summary>
        public int GetPaymentDeadlineDays()
        {
            var value = GetConfigValue("PAYMENT_DEADLINE_DAYS", "7");
            return int.TryParse(value, out int days) ? days : 7;
        }

        /// <summary>
        /// 設定繳費期限天數
        /// </summary>
        public void SetPaymentDeadlineDays(int days)
        {
            if (days < 1) days = 1;
            if (days > 30) days = 30;
            SetConfigValue("PAYMENT_DEADLINE_DAYS", days.ToString());
        }

        // ========== 3. 最早預約天數 ==========

        /// <summary>
        /// 取得最早預約天數
        /// </summary>
        public int GetMinAdvanceBookingDays()
        {
            var value = GetConfigValue("MIN_ADVANCE_BOOKING_DAYS", "7");
            return int.TryParse(value, out int days) ? days : 7;
        }

        /// <summary>
        /// 設定最早預約天數
        /// </summary>
        public void SetMinAdvanceBookingDays(int days)
        {
            if (days < 0) days = 0;
            if (days > 30) days = 30;
            SetConfigValue("MIN_ADVANCE_BOOKING_DAYS", days.ToString());
        }

        // ========== 4. 主管信箱 ==========

        /// <summary>
        /// 取得主管信箱
        /// </summary>
        public string GetManagerEmail()
        {
            return GetConfigValue("MANAGER_EMAIL", "");
        }

        /// <summary>
        /// 設定主管信箱
        /// </summary>
        public void SetManagerEmail(string email)
        {
            SetConfigValue("MANAGER_EMAIL", email ?? "");
        }

        // ========== 5. 是否自動釋放逾期未繳費預約 ==========

        /// <summary>
        /// 取得是否自動釋放逾期預約
        /// </summary>
        public bool IsAutoReleaseEnabled()
        {
            var value = GetConfigValue("AUTO_RELEASE_AFTER_DEADLINE", "true");
            return value.ToLower() == "true";
        }

        /// <summary>
        /// 設定是否自動釋放逾期預約
        /// </summary>
        public void SetAutoRelease(bool enabled)
        {
            SetConfigValue("AUTO_RELEASE_AFTER_DEADLINE", enabled.ToString().ToLower());
        }
    }
}
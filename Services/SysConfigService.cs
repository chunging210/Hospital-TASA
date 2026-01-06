using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services
{
    public class SysConfigService(TASAContext db, ServiceWrapper service) : IService
    {
        /// <summary>
        /// 取得「是否開放註冊」
        /// </summary>
        public bool IsRegistrationOpen()
        {
            var config = db.SysConfig
                .AsNoTracking()
                .FirstOrDefault(x => 
                    x.ConfigKey == "GUEST_REGISTRATION" && 
                    x.DeleteAt == null);

            if (config == null)
            {
                return true; // ✅ 預設開放
            }

            return config.ConfigValue.ToLower() == "true";
        }

        /// <summary>
        /// 設定「是否開放註冊」
        /// </summary>
        public void SetRegistrationOpen(bool isOpen)
        {
            var config = db.SysConfig
                .FirstOrDefault(x => 
                    x.ConfigKey == "GUEST_REGISTRATION" && 
                    x.DeleteAt == null);

            if (config == null)
            {
                // ✅ 不存在就新增
                config = new SysConfig
                {
                    Id = Guid.NewGuid(),
                    ConfigKey = "GUEST_REGISTRATION",
                    ConfigValue = isOpen.ToString().ToLower(),
                    Enabled = true
                };
                db.SysConfig.Add(config);
                db.SaveChanges();
                _ = service.LogServices.LogAsync("系統設定新增", $"GUEST_REGISTRATION: {isOpen}");
            }
            else
            {
                // ✅ 存在就更新
                var oldValue = config.ConfigValue;
                config.ConfigValue = isOpen.ToString().ToLower();
                db.SaveChanges();
                _ = service.LogServices.LogAsync("系統設定編輯", $"GUEST_REGISTRATION: {oldValue} -> {isOpen}");
            }
        }
    }
}
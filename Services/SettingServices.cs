using TASA.Program;

namespace TASA.Services
{
    public class SettingServices(IConfiguration configuration) : IService
    {
        public class SettingsModel
        {
            public class UCMSSettings
            {
                public bool Webex { get; set; } = false;
                public bool Ecs { get; set; } = false;
                public int DelayStartTime { get; set; } = 30;
                public int BeforeStart { get; set; } = 10;

            }
            [ConfigurationKeyName("UCMS")]
            public UCMSSettings UCNS { set; get; } = new UCMSSettings();
        }

        private readonly Lazy<SettingsModel> _lazySettings = new(() =>
        {
            var settings = new SettingsModel();
            configuration.Bind(settings);
            return settings;
        });

        public SettingsModel GetSettings()
        {
            return _lazySettings.Value;
        }
    }
}

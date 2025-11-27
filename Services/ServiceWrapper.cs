using TASA.Services.AuthModule;
using TASA.Services.AuthUserModule;
using TASA.Services.ConferenceModule;
using TASA.Services.ConferenceTemplateMoule;
using TASA.Services.DepartmentModule;
using TASA.Services.EcsModule;
using TASA.Services.EquipmentModule;
using TASA.Services.MailModule;
using TASA.Services.RoomModule;
using TASA.Services.SeatSettingModule;
using TASA.Services.VisitorModule;
using TASA.Services.WebexModule;

namespace TASA.Services
{
    public class ServiceWrapper(
        Lazy<IWebHostEnvironment> environment,

        Lazy<LogServices> logServices,
        Lazy<SettingServices> settingServices,
        Lazy<SelectServices> selectServices,

        Lazy<LoginServices> loginServices,
        Lazy<PasswordServices> passwordServices,
        Lazy<PasswordMail> passwordMail,
        Lazy<UserClaimsService> userClaimsService,

        Lazy<ProfilesServices> profilesServices,
        Lazy<AuthUserServices> authUserServices,
        Lazy<AuthRoleServices> authRoleServices,

        Lazy<RoomService> roomService,
        Lazy<DepartmentService> departmentService,
        Lazy<ConferenceService> conferenceService,
        Lazy<JobService> jobService,
        Lazy<ConferenceMail> conferenceMail,
        Lazy<ConferenceTemplateService> conferenceTemplateService,
        Lazy<CalendarService> calendarService,

        Lazy<EquipmentService> equipmentService,
        Lazy<EcsService> ecsService,
        Lazy<TcpCommandService> tcpCommandService,

        Lazy<WebexService> webexService,
        Lazy<WebexMeetingService> webexMeetingService,
        Lazy<SeatSettingService> seatSettingService,
        Lazy<VisitorService> visitorService

    )
    {
        public IWebHostEnvironment Environment => environment.Value;

        public LogServices LogServices => logServices.Value;
        public SettingServices SettingServices => settingServices.Value;
        public SelectServices SelectServices => selectServices.Value;

        public LoginServices LoginServices => loginServices.Value;
        public PasswordServices PasswordServices => passwordServices.Value;
        public PasswordMail PasswordMail => passwordMail.Value;
        public UserClaimsService UserClaimsService => userClaimsService.Value;

        public ProfilesServices ProfilesServices => profilesServices.Value;
        public AuthUserServices AuthUserServices => authUserServices.Value;
        public AuthRoleServices AuthRoleServices => authRoleServices.Value;

        public RoomService RoomService => roomService.Value;
        public DepartmentService DepartmentService => departmentService.Value;
        public ConferenceService ConferenceService => conferenceService.Value;
        public JobService JobService => jobService.Value;
        public ConferenceMail ConferenceMail => conferenceMail.Value;
        public ConferenceTemplateService ConferenceTemplateService => conferenceTemplateService.Value;
        public CalendarService CalendarService => calendarService.Value;

        public EquipmentService EquipmentService => equipmentService.Value;
        public EcsService EcsService => ecsService.Value;
        public TcpCommandService TcpCommandService => tcpCommandService.Value;

        public WebexService WebexService => webexService.Value;
        public WebexMeetingService WebexMeetingService => webexMeetingService.Value;

        public SeatSettingService SeatSettingService => seatSettingService.Value;

        public VisitorService VisitorService => visitorService.Value;
    }
}

using TASA.Services.AuthModule;
using TASA.Services.AuthUserModule;
using TASA.Services.ConferenceModule;
using TASA.Services.DepartmentModule;
using TASA.Services.EquipmentModule;
using TASA.Services.MailModule;
using TASA.Services.RoomModule;
using TASA.Services.DelegateModule;
using TASA.Services.CaptchaModule;
using TASA.Services.HolidayModule;
using TASA.Services.ReportModule;
using TASA.Services.CostCenterModule;
using TASA.Services.StatisticsModule;
using TASA.Services.AnnouncementModule;
using TASA.Services.NameplateModule;
using TASA.Services;

namespace TASA.Services
{
    public class ServiceWrapper(
        Lazy<IWebHostEnvironment> environment,

        Lazy<LogServices> logServices,
        Lazy<SettingServices> settingServices,
        Lazy<SelectServices> selectServices,

        Lazy<LoginServices> loginServices,
        Lazy<LoginLogServices> loginLogServices,
        Lazy<RegisterServices> registerServices,
        Lazy<PasswordServices> passwordServices,
        Lazy<PasswordMail> passwordMail,
        Lazy<UserClaimsService> userClaimsService,
        Lazy<UserContextService> userContextService,

        Lazy<ProfilesServices> profilesServices,
        Lazy<AuthUserServices> authUserServices,
        Lazy<AuthRoleServices> authRoleServices,

        Lazy<RoomService> roomService,
        Lazy<DepartmentService> departmentService,
        Lazy<PaymentService> paymentService,
        Lazy<ReservationService> reservationService,
        Lazy<ConferenceMail> conferenceMail,
        Lazy<CalendarService> calendarService,

        Lazy<EquipmentService> equipmentService,

        Lazy<SysConfigService> sysConfigService,
        Lazy<ReservationAutoManagementService> reservationAutoManagementService,
        Lazy<RoomManagerDelegateService> roomManagerDelegateService,
        Lazy<CaptchaService> captchaService,
        Lazy<RoomApprovalLevelService> roomApprovalLevelService,
        Lazy<HolidayService> holidayService,
        Lazy<ReportService> reportService,
        Lazy<CostCenterManagerService> costCenterManagerService,
        Lazy<StatisticsService> statisticsService,
        Lazy<AnnouncementService> announcementService,
        Lazy<NameplateService> nameplateService

    )
    {
        public IWebHostEnvironment Environment => environment.Value;

        public LogServices LogServices => logServices.Value;
        public SettingServices SettingServices => settingServices.Value;
        public SelectServices SelectServices => selectServices.Value;

        public LoginServices LoginServices => loginServices.Value;
        public LoginLogServices LoginLogServices => loginLogServices.Value;
        public PasswordServices PasswordServices => passwordServices.Value;
        public RegisterServices RegisterServices => registerServices.Value;
        public PasswordMail PasswordMail => passwordMail.Value;
        public UserClaimsService UserClaimsService => userClaimsService.Value;
        public UserContextService UserContextService => userContextService.Value;

        public ProfilesServices ProfilesServices => profilesServices.Value;
        public AuthUserServices AuthUserServices => authUserServices.Value;
        public AuthRoleServices AuthRoleServices => authRoleServices.Value;

        public RoomService RoomService => roomService.Value;
        public DepartmentService DepartmentService => departmentService.Value;
        public PaymentService PaymentService => paymentService.Value;
        public ReservationService ReservationService => reservationService.Value;
        public ConferenceMail ConferenceMail => conferenceMail.Value;
        public CalendarService CalendarService => calendarService.Value;

        public EquipmentService EquipmentService => equipmentService.Value;

        public SysConfigService SysConfigService => sysConfigService.Value;

        public ReservationAutoManagementService ReservationAutoManagementService => reservationAutoManagementService.Value;

        public RoomManagerDelegateService RoomManagerDelegateService => roomManagerDelegateService.Value;

        public CaptchaService CaptchaService => captchaService.Value;

        public RoomApprovalLevelService RoomApprovalLevelService => roomApprovalLevelService.Value;

        public HolidayService HolidayService => holidayService.Value;

        public ReportService ReportService => reportService.Value;

        public CostCenterManagerService CostCenterManagerService => costCenterManagerService.Value;

        public StatisticsService StatisticsService => statisticsService.Value;

        public AnnouncementService AnnouncementService => announcementService.Value;

        public NameplateService NameplateService => nameplateService.Value;

    }
}

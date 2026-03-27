using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using TASA.Program.ModelState;
using TASA.Models.Enums;
using static TASA.Services.ConferenceModule.ConferenceService.InsertVM;

namespace TASA.Services.ConferenceModule
{
    public class ConferenceService(TASAContext db, ServiceWrapper service) : IService
    {
        public SettingServices.SettingsModel.UCMSSettings ConferenceSettings { get { return service.SettingServices.GetSettings().UCNS; } }
        public DateTime PreparationTime { get { return DateTime.Now.AddMinutes(ConferenceSettings.BeforeStart); } }


        public record InsertVM
        {
            // ===== 共用欄位 =====
            public Guid? Id { get; set; }

            [RequiredI18n(ErrorMessage = "會議名稱是必要項")]
            public string? Name { get; set; }

            [RequiredI18n(ErrorMessage = "會議類型是必要項")]
            public byte? UsageType { get; set; }

            public string? Description { get; set; }

            [RequiredI18n(ErrorMessage = "承辦單位是必要項")]
            public string? OrganizerUnit { get; set; }  // 承辦單位

            [RequiredI18n(ErrorMessage = "會議主席是必要項")]
            public string? Chairman { get; set; }       // 會議主席

            public int? ExpectedAttendees { get; set; } // ✅ 預計到達人數

            [RequiredI18n(ErrorMessage = "聯絡電話是必要項")]
            public string? ContactPhone { get; set; }   // 聯絡電話

            [RequiredI18n(ErrorMessage = "電子郵件是必要項")]
            public string? ContactEmail { get; set; }   // 電子郵件

            // ===== 舊系統(即時會議/傳統會議)專用 =====
            public DateTime? StartTime { get; set; }
            public bool StartNow { get; set; }

            [RequiredI18n(ErrorMessage = "持續時間是必要項")]
            public byte? DurationHH { get; set; }

            [RequiredI18n(ErrorMessage = "持續時間是必要項")]
            public byte? DurationSS { get; set; }

            public string? RRule { get; set; }
            public List<Guid> Room { get; set; } = [];  // 舊系統:多個會議室
            public byte? MCU { get; set; }
            public bool Recording { get; set; }

            // ===== 新預約系統專用 =====
            public Guid? RoomId { get; set; }  // 新系統:單一會議室
            public DateTime? ReservationDate { get; set; }  // 預約日期（單日模式）
            public DateTime? StartDate { get; set; }  // 開始日期（跨日模式）
            public DateTime? EndDate { get; set; }    // 結束日期（跨日模式）
            public List<string>? SlotKeys { get; set; }     // 時段陣列 ["09:00-10:00", "10:00-11:00"]（向後相容）
            public List<SlotInfoVM>? SlotInfos { get; set; }  // 新格式: 包含 isSetup 資訊

            public record SlotInfoVM
            {
                public string Key { get; set; } = string.Empty;  // 時段 Key "09:00:00-10:00:00"
                public bool IsSetup { get; set; } = false;       // 是否為場佈
                public string? Date { get; set; }                // 日期 "2025-01-20"（跨日模式用）
            }
            public List<Guid>? EquipmentIds { get; set; } = [];
            public List<Guid>? BoothIds { get; set; } = [];
            public List<SmallBoothVM>? SmallBooths { get; set; } = [];

            public record SmallBoothVM
            {
                public Guid BoothId { get; set; }
                public int Quantity { get; set; }
            }
            public decimal? RoomCost { get; set; }
            public decimal? EquipmentCost { get; set; }
            public decimal? BoothCost { get; set; }
            public decimal? SmallBoothCost { get; set; }  // ✅ 小型攤位費用
            public int? ParkingTicketCount { get; set; }
            public int? ParkingTicketCost { get; set; }
            public decimal? TotalAmount { get; set; }
            public string? PaymentMethod { get; set; }
            public string? DepartmentCode { get; set; }
            public Guid? DepartmentId { get; set; }

            // ===== 共用欄位 =====
            public List<Guid> User { get; set; } = [];
            public List<Guid> Department { get; set; } = [];
            public Guid? Host { get; set; }
            public Guid? Recorder { get; set; }
            public List<Guid>? AttendeeIds { get; set; }
            public string? ReservationNo { get; set; }

            // ===== 循環預約（僅院內人員可用） =====
            public bool IsRecurring { get; set; } = false;
            public int? RecurrenceKind { get; set; }   // 1=每日, 2=每週, 3=每月
            public List<int>? RecurrenceDaysOfWeek { get; set; }  // 0=日, 1=一, ..., 6=六（每週用）
            public int? RecurrenceDayOfMonth { get; set; }         // 1-31（每月用）
            public DateTime? RecurrenceEndDate { get; set; }       // 循環結束日期

            public List<AttachmentVM>? Attachments { get; set; }

            public record AttachmentVM
            {
                public AttachmentType Type { get; set; }  // 1=議程表, 2=會議文件
                public string FileName { get; set; } = string.Empty;
                public string Base64Data { get; set; } = string.Empty;  // 或用 IFormFile
            }
        }

        public record ListVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public byte DurationHH { get; set; }
            public byte DurationSS { get; set; }
            public string? Host { get; set; }
            public IEnumerable<string> Room { get; set; } = [];
            public IEnumerable<string> ConferenceUser { get; set; } = [];
            public IEnumerable<string> Department { get; set; } = [];
            public Guid CreateBy { get; set; }
            public string CreateByName { get; set; } = string.Empty;
            public byte Status { get; set; }
            public bool CanEdit { get; set; }
            public bool Recording7 { get; set; }
            public bool ZeroTouch { get; set; }

            public string? OrganizerUnit { get; set; }
            public string? Chairman { get; set; }

            // 新欄位：支援新預約系統
            public string? RoomLocation { get; set; }
            public DateOnly? SlotDate { get; set; }
            public DateOnly? SlotDateEnd { get; set; }  // ✅ 跨日預約結束日期
            public TimeOnly? SlotStart { get; set; }
            public TimeOnly? SlotEnd { get; set; }
            public int SlotCount { get; set; }
            public bool IsMultiDay => SlotDate.HasValue && SlotDateEnd.HasValue && SlotDate != SlotDateEnd;  // ✅ 是否為跨日
        }

        /// <summary>
        /// 列表 - 顯示所有會議（含舊系統與新預約系統）
        /// 新預約系統：顯示時段被鎖住的會議（排除已取消、審核拒絕）
        /// 舊系統：顯示有 StartTime 的會議
        /// </summary>
        public IQueryable<ListVM> List(BaseQueryVM query)
        {
            return db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x =>
                    // 舊系統會議：有 StartTime 且未取消/拒絕
                    (x.StartTime.HasValue &&
                     x.ReservationStatus != ReservationStatus.Cancelled &&
                     x.ReservationStatus != ReservationStatus.Rejected) ||
                    // 新預約系統：只要時段被鎖住就顯示（排除已取消、審核拒絕）
                    (!x.StartTime.HasValue &&
                     x.ReservationStatus != ReservationStatus.Cancelled &&
                     x.ReservationStatus != ReservationStatus.Rejected)
                )
                .WhereIf(query.Start.HasValue, x =>
                    (x.StartTime.HasValue && query.Start <= x.StartTime) ||
                    (!x.StartTime.HasValue && x.ConferenceRoomSlots.Any(s => s.SlotDate >= DateOnly.FromDateTime(query.Start!.Value.Date))))
                .WhereIf(query.End.HasValue, x =>
                    (x.StartTime.HasValue && x.StartTime <= query.End) ||
                    (!x.StartTime.HasValue && x.ConferenceRoomSlots.Any(s => s.SlotDate <= DateOnly.FromDateTime(query.End!.Value.Date))))
                .WhereIf(query.RoomId.HasValue, x =>
                    x.Room.Any(y => y.Id == query.RoomId) ||
                    x.ConferenceRoomSlots.Any(s => s.RoomId == query.RoomId))
                .WhereIf(!string.IsNullOrWhiteSpace(query.Building), x =>
                    x.Room.Any(y => y.Building == query.Building) ||
                    x.ConferenceRoomSlots.Any(s => s.Room.Building == query.Building))
                .WhereIf(query.UserId.HasValue, x => x.CreateBy == query.UserId)
                .WhereIf(!string.IsNullOrWhiteSpace(query.DepartmentCode), x => x.DepartmentCode == query.DepartmentCode)
                .WhereIf(query.Keyword, x => x.Name.Contains(query.Keyword!))
                .OrderBy(x => x.ConferenceRoomSlots.Any()
                    ? x.ConferenceRoomSlots.Min(s => s.SlotDate).ToDateTime(TimeOnly.MinValue)
                    : (x.StartTime ?? x.CreateAt))
                .Mapping(x => new ListVM()
                {
                    Host = x.ConferenceUser.Where(y => y.IsHost).Select(y => y.User.Name).FirstOrDefault(),
                    Room = x.Room.Select(y => y.Name).ToList(),
                    ConferenceUser = x.ConferenceUser.Select(y => y.User.Name).ToList(),
                    Department = x.Department.Select(y => y.Name).ToList(),
                    CreateByName = x.CreateByNavigation.Name,
                    CanEdit = x.StartTime.HasValue && x.StartTime > PreparationTime,
                    Recording7 = x.Status == 4 && x.MCU == 7,

                    // 新預約系統欄位
                    RoomLocation = x.ConferenceRoomSlots.Any()
                        ? x.ConferenceRoomSlots
                            .OrderBy(s => s.StartTime)
                            .Select(s => s.Room.Building + " " + s.Room.Floor + "樓 " + s.Room.Name)
                            .FirstOrDefault()
                        : x.Room.Select(y => y.Name).FirstOrDefault(),
                    SlotDate = x.ConferenceRoomSlots.Any()
                        ? x.ConferenceRoomSlots.Min(s => (DateOnly?)s.SlotDate)
                        : null,
                    SlotDateEnd = x.ConferenceRoomSlots.Any()
                        ? x.ConferenceRoomSlots.Max(s => (DateOnly?)s.SlotDate)
                        : null,
                    SlotStart = x.ConferenceRoomSlots.Any()
                        ? x.ConferenceRoomSlots.Min(s => (TimeOnly?)s.StartTime)
                        : null,
                    SlotEnd = x.ConferenceRoomSlots.Any()
                        ? x.ConferenceRoomSlots.Max(s => (TimeOnly?)s.EndTime)
                        : null,
                    SlotCount = x.ConferenceRoomSlots.Count(),
                });
        }

        public record DetailVM
        {
            public record RoomVM : IdNameVM
            {
            }
            public record UserVM : IdNameVM
            {
                public bool IsAttendees { get; set; }
                public bool IsHost { get; set; }
                public bool IsRecorder { get; set; }
            }

            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public byte UsageType { get; set; }
            public byte? MCU { get; set; }
            public bool Recording { get; set; }
            public string Description { get; set; } = string.Empty;
            public DateTime? StartTime { get; set; }
            public bool StartNow { get; set; }
            public DateTime? EndTime { get; set; }
            public byte DurationHH { get; set; }
            public byte DurationSS { get; set; }
            public string RRule { get; set; } = string.Empty;
            public IEnumerable<RoomVM> Room { get; set; } = [];
            public IEnumerable<UserVM> User { get; set; } = [];
            public IEnumerable<IdNameVM> Department { get; set; } = [];
            public string CreateBy { get; set; } = string.Empty;
            public bool CanEdit { get; set; }
        }

        /// <summary>
        /// 詳細資料 - 只查舊系統會議
        /// </summary>
        public DetailVM? Detail(Guid id)
        {
            var detail = db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x => x.StartTime.HasValue && x.EndTime.HasValue)
                .Mapping(x => new DetailVM()
                {
                    Room = x.Room.Select(x => new DetailVM.RoomVM()
                    {
                        Id = x.Id,
                        Name = x.Name
                    }),
                    User = x.ConferenceUser.Select(x => new DetailVM.UserVM()
                    {
                        Id = x.User.Id,
                        Name = x.User.Name,
                        IsAttendees = x.IsAttendees,
                        IsHost = x.IsHost,
                        IsRecorder = x.IsRecorder
                    }),
                    Department = x.Department.Select(x => new IdNameVM()
                    {
                        Id = x.Id,
                        Name = x.Name
                    }),
                    CreateBy = x.CreateByNavigation.Name,
                    CanEdit = x.StartTime.HasValue && x.StartTime > PreparationTime
                })
                .FirstOrDefault(x => x.Id == id);

            return detail;
        }

        /// <summary>
        /// 新增 - 自動路由到舊系統或新預約系統
        /// </summary>
        public Guid Insert(InsertVM vm)
        {
            // ✅ 路由邏輯:如果有 SlotKeys,導向新預約系統
            if (vm.SlotKeys != null && vm.SlotKeys.Any())
            {
                return service.ReservationService.CreateReservation(vm);
            }

            // ===== 以下是舊系統邏輯 =====

            // 舊系統必須有 StartTime (除非是 StartNow)
            if (!vm.StartNow && !vm.StartTime.HasValue)
            {
                throw new HttpException("必須指定會議開始時間");
            }

            // 如果前端送的是 AttendeeIds,轉換為 User 陣列
            if (!vm.User.Any() && vm.Room.Any())
            {
                vm.User = new List<Guid> { service.UserClaimsService.Me()?.Id ?? Guid.Empty };
                vm.Host = vm.User.FirstOrDefault();
            }

            // 驗證資料
            if (db.Conference.WhereNotDeleted().Any(x => x.Name == vm.Name && x.StartTime == vm.StartTime))
            {
                throw new HttpException(I18nMessgae.DataExists);
            }

            // 計算開始和結束時間
            var StartTime = vm.StartNow
                ? DateTime.Now.AddSeconds(ConferenceSettings.DelayStartTime)
                : vm.StartTime!.Value;

            var EndTime = StartTime
                .AddHours(vm.DurationHH!.Value)
                .AddMinutes(vm.DurationSS!.Value);

            // 檢查會議室衝突
            var used = db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x => x.Id != vm.Id
                         && x.StartTime.HasValue
                         && x.EndTime.HasValue
                         && x.StartTime <= EndTime
                         && StartTime <= x.EndTime)
                .SelectMany(x => x.Room)
                .Where(x => vm.Room.Contains(x.Id))
                .Distinct()
                .ToDictionary(x => x.Name, x => stringArray);

            if (used.Count > 0)
            {
                throw new HttpException(used);
            }

            // 建立會議記錄
            var conferenceId = Guid.NewGuid();
            var userId = service.UserClaimsService.Me()?.Id;

            var data = new Conference
            {
                Id = conferenceId,
                Name = vm.Name,
                UsageType = vm.UsageType!.Value,
                MCU = vm.UsageType == 2 ? vm.MCU : null,
                Recording = vm.UsageType == 2 && vm.Recording,
                Description = vm.Description,
                StartTime = StartTime,
                DurationHH = vm.DurationHH!.Value,
                DurationSS = vm.DurationSS!.Value,
                EndTime = EndTime,
                RRule = vm.RRule,
                Room = [.. db.SysRoom.Where(x => vm.Room.Contains(x.Id))],
                ConferenceUser = GetUsers(vm.User, vm.Host, vm.Recorder),
                Department = [.. db.SysDepartment.Where(x => vm.Department.Contains(x.Id))],
                CreateBy = userId!.Value,
                CreateAt = DateTime.Now,
            };

            data.Status = GetStatus(vm.StartNow, data);

            db.Conference.Add(data);
            db.SaveChanges();

            service.ConferenceMail.New(data);
            _ = service.LogServices.LogAsync("會議新增", $"{data.Name}({data.Id})");

            return data.Id;
        }

        /// <summary>
        /// 編輯
        /// </summary>
        public void Update(InsertVM vm)
        {
            if (db.Conference.WhereNotDeleted().Any(x => x.Id != vm.Id && x.Name == vm.Name && x.StartTime == vm.StartTime))
            {
                throw new HttpException(I18nMessgae.DataExists);
            }

            var StartTime = vm.StartNow ? DateTime.Now.AddSeconds(ConferenceSettings.DelayStartTime) : vm.StartTime!.Value;
            var EndTime = StartTime.AddHours(vm.DurationHH!.Value).AddMinutes(vm.DurationSS!.Value);

            var used = db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x => x.Id != vm.Id
                         && x.StartTime.HasValue
                         && x.EndTime.HasValue
                         && x.StartTime <= EndTime
                         && StartTime <= x.EndTime)
                .SelectMany(x => x.Room)
                .Where(x => vm.Room.Contains(x.Id))
                .Distinct()
                .ToDictionary(x => x.Name, x => stringArray);

            if (used.Count > 0)
            {
                throw new HttpException(used);
            }

            var data = db.Conference
                .Include(x => x.Room)
                .Include(x => x.ConferenceUser)
                .Include(x => x.Department)
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id) ?? throw new HttpException(I18nMessgae.DataNotFound);

            data.Name = vm.Name;
            data.UsageType = vm.UsageType!.Value;
            data.MCU = vm.UsageType == 2 ? vm.MCU : null;
            data.Recording = vm.UsageType == 2 && vm.Recording;
            data.Description = vm.Description;
            data.StartTime = StartTime;
            data.DurationHH = vm.DurationHH!.Value;
            data.DurationSS = vm.DurationSS!.Value;
            data.EndTime = EndTime;
            data.Room = [.. db.SysRoom.Where(x => vm.Room.Contains(x.Id))];
            data.ConferenceUser = GetUsers(vm.User, vm.Host, vm.Recorder);
            data.Department = [.. db.SysDepartment.Where(x => vm.Department.Contains(x.Id))];
            data.Status = GetStatus(vm.StartNow, data);

            db.SaveChanges();

            service.ConferenceMail.New(data, "[會議修改通知]");
            _ = service.LogServices.LogAsync("會議編輯", $"{data.Name}({data.Id})");
        }

        private static readonly string[] stringArray = ["已被預約"];

        private static List<ConferenceUser> GetUsers(IEnumerable<Guid> attendees, Guid? host, Guid? recorder)
        {
            var users = attendees.Select(x => new ConferenceUser { UserId = x, IsAttendees = true, }).ToList();
            if (host.HasValue)
            {
                var user = users.FirstOrDefault(x => x.UserId == host.Value);
                if (user == null)
                {
                    user = new ConferenceUser { UserId = host.Value };
                    users.Add(user);
                }
                user.IsHost = true;
            }
            if (recorder.HasValue)
            {
                var user = users.FirstOrDefault(x => x.UserId == recorder.Value);
                if (user == null)
                {
                    user = new ConferenceUser { UserId = recorder.Value };
                    users.Add(user);
                }
                user.IsRecorder = true;
            }
            return users;
        }

        /// <summary>
        /// 取得會議狀態
        /// </summary>
        public byte GetStatus(bool startNow, Conference conference)
        {


            if (conference.StartTime == null || conference.EndTime == null)
            {
                return 1;
            }

            if (DateTime.Now > (conference.FinishTime?.ToUniversalTime() ?? conference.EndTime!.Value.ToUniversalTime()))
            {
                return 4;
            }
            if (startNow || DateTime.Now > conference.StartTime!.Value.ToUniversalTime())
            {
                return 3;
            }
            if (DateTime.Now.AddMinutes(ConferenceSettings.BeforeStart) > conference.StartTime!.Value.ToUniversalTime())
            {
                return 2;
            }
            return 1;
        }

        /// <summary>
        /// 提早結束
        /// </summary>
        public void End(Guid id)
        {
            var data = db.Conference
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.FinishTime = DateTime.Now;
                data.Status = 4;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("會議結束", $"{data.Name}({data.Id})");
            }
        }

        /// <summary>
        /// 刪除
        /// </summary>
        public void Delete(Guid id)
        {
            var data = db.Conference
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.DeleteAt = DateTime.Now;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("會議刪除", $"{data.Name}({data.Id})");
            }
        }
    }
}
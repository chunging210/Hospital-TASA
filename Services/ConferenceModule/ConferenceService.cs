using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using TASA.Program.ModelState;
using static TASA.Services.ConferenceModule.ConferenceService.InsertVM;

namespace TASA.Services.ConferenceModule
{
    public class ConferenceService(TASAContext db, ServiceWrapper service) : IService
    {
        public SettingServices.SettingsModel.UCMSSettings ConferenceSettings { get { return service.SettingServices.GetSettings().UCNS; } }
        public DateTime PreparationTime { get { return DateTime.UtcNow.AddMinutes(ConferenceSettings.BeforeStart); } }


        public record ListVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            // ✅ 改為 nullable
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
        }
        /// <summary>
        /// 列表
        /// ⚠️ 臨時防呆：只查詢 StartTime/EndTime 都有值的會議（舊方式）
        /// </summary>
        public IQueryable<ListVM> List(BaseQueryVM query)
        {
            return db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                // ✅ 防呆：只查舊會議（有 StartTime/EndTime）
                .Where(x => x.StartTime.HasValue && x.EndTime.HasValue)
                .WhereIf(query.Start.HasValue, x => query.Start <= x.StartTime)
                .WhereIf(query.End.HasValue, x => x.StartTime <= query.End)
                .WhereIf(query.RoomId.HasValue, x => x.Room.Any(y => y.Id == query.RoomId))
                .WhereIf(query.DepartmentId.HasValue, x => x.Department.Any(y => y.Id == query.DepartmentId))
                .WhereIf(query.UserId.HasValue, x => x.CreateBy == query.UserId)
                .WhereIf(query.Keyword, x => x.Name.Contains(query.Keyword!))
                .OrderByDescending(x => x.StartTime)
                .Mapping(x => new ListVM()
                {
                    Host = x.ConferenceUser.Where(y => y.IsHost).Select(y => y.User.Name).FirstOrDefault(),
                    Room = x.Room.Select(y => y.Name).ToList(),
                    ConferenceUser = x.ConferenceUser.Select(y => y.User.Name).ToList(),
                    Department = x.Department.Select(y => y.Name).ToList(),
                    CreateByName = x.CreateByNavigation.Name,
                    // ✅ 修復：檢查是否有值後才比較
                    CanEdit = x.StartTime.HasValue && x.StartTime > PreparationTime,
                    Recording7 = x.Status == 4 && x.MCU == 7,
                    //ZeroTouch = x.Status == 3 && x.MCU == 7 && x.Room.SelectMany(y => y.RoomEquipment).Any(y => y.Type == 1 || y.Type == 2),
                });
        }

        public record DetailVM
        {
            public record RoomVM : IdNameVM
            {
                public IEnumerable<IdNameVM> Ecs { get; set; } = [];
            }
            public record UserVM : IdNameVM
            {
                public bool IsAttendees { get; set; }
                public bool IsHost { get; set; }
                public bool IsRecorder { get; set; }
            }

            public record VisitorVM : IdNameVM
            {
                public string Email { get; set; } = string.Empty;
                public string CompanyName { get; set; } = string.Empty;
                public string Phone { get; set; } = string.Empty;
            }

            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public byte UsageType { get; set; }
            public byte? MCU { get; set; }
            public bool Recording { get; set; }
            public string Description { get; set; } = string.Empty;
            // ✅ 改為 nullable
            public DateTime? StartTime { get; set; }
            public bool StartNow { get; set; }
            public DateTime? EndTime { get; set; }
            public byte DurationHH { get; set; }
            public byte DurationSS { get; set; }
            public string RRule { get; set; } = string.Empty;
            public IEnumerable<RoomVM> Room { get; set; } = [];
            public IEnumerable<UserVM> User { get; set; } = [];
            public IEnumerable<IdNameVM> Department { get; set; } = [];
            public IEnumerable<VisitorVM> Visitor { get; set; } = [];
            public string CreateBy { get; set; } = string.Empty;
            public ConferenceWebex? Webex { get; set; }
            public bool CanEdit { get; set; }
        }

        /// <summary>
        /// 詳細資料
        /// ⚠️ 臨時防呆：只查詢 StartTime/EndTime 都有值的會議（舊方式）
        /// </summary>        
        public DetailVM? Detail(Guid id)
        {
            var detail = db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                // ✅ 防呆：只查舊會議（有 StartTime/EndTime）
                .Where(x => x.StartTime.HasValue && x.EndTime.HasValue)
                .Mapping(x => new DetailVM()
                {
                    Room = x.Room.Select(x => new DetailVM.RoomVM()
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Ecs = x.Ecs.Select(y => new IdNameVM() { Id = y.Id, Name = y.Name })
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
                    // ✅ 修復：檢查是否有值後才比較
                    CanEdit = x.StartTime.HasValue && x.StartTime > PreparationTime,
                    Webex = x.ConferenceWebex
                })
                .FirstOrDefault(x => x.Id == id);

            if (detail == null)
                return null;

            // ✅ 取得訪客資料
            var visitors = db.ConferenceVisitor
                .Where(cv => cv.ConferenceId == id)
                .Join(
                    db.Visitor.WhereNotDeleted(),
                    cv => cv.VisitorId,
                    v => v.Id,
                    (cv, v) => new DetailVM.VisitorVM
                    {
                        Id = v.Id,
                        Name = v.CName,
                        Email = v.Email ?? string.Empty,
                        CompanyName = v.CompanyName ?? string.Empty,
                        Phone = v.Phone ?? string.Empty,
                    }
                )
                .ToList();

            detail = detail with { Visitor = visitors };

            return detail;
        }
        public record InsertVM
        {
            public Guid? Id { get; set; }
            [RequiredI18n(ErrorMessage = "會議名稱是必要項")]
            public string? Name { get; set; }
            [RequiredI18n(ErrorMessage = "會議類型是必要項")]
            public byte? UsageType { get; set; }
            public byte? MCU { get; set; }
            public bool Recording { get; set; }
            public string? Description { get; set; }

            public DateTime? StartTime { get; set; }
            public bool StartNow { get; set; }
            [RequiredI18n(ErrorMessage = "持續時間是必要項")]
            public byte? DurationHH { get; set; }
            [RequiredI18n(ErrorMessage = "持續時間是必要項")]
            public byte? DurationSS { get; set; }
            public string? RRule { get; set; }
            
            [RequiredI18n(ErrorMessage = "與會地點是必要項")]
            public Guid? RoomId { get; set; }

            public List<Guid> Room { get; set; } = [];
            public List<Guid> Ecs { get; set; } = [];
            public List<Guid> User { get; set; } = [];
            public List<Guid> Department { get; set; } = [];
            public Guid? Host { get; set; }
            public Guid? Recorder { get; set; }

            public List<Guid> Guests { get; set; } = [];  // 從 Visitor 表選的
            public List<GuestManualVM> GuestsManual { get; set; } = [];  // 臨時新增的

            public List<string>? SlotKeys { get; set; }   // 前端送的時段 Key 陣列
            public List<Guid>? EquipmentIds { get; set; } = [];  // 設備 ID
            public List<Guid>? BoothIds { get; set; } = [];      // 攤位 ID
            public decimal? RoomCost { get; set; }        // 會議室費用（暫存，用於日誌）
            public decimal? EquipmentCost { get; set; }   // 設備費用（暫存，用於日誌）
            public decimal? BoothCost { get; set; }       // 攤位費用（暫存，用於日誌）
            public decimal? TotalAmount { get; set; }     // 總金額（暫存，用於日誌）
            public string? PaymentMethod { get; set; }    // 付款方式
            public string? DepartmentCode { get; set; }   // 部門代碼（成本分攤用）
            public List<Guid>? AttendeeIds { get; set; }  // ✅ 【新增】

            public record GuestManualVM
            {
                [RequiredI18n]
                [StringLength(100)]
                public string CName { get; set; } = string.Empty;

                [RequiredI18n]
                [StringLength(100)]
                public string CompanyName { get; set; } = string.Empty;

                [RequiredI18n]
                [RegularExpression(@"^\d{10}$", ErrorMessage = "電話需為 10 碼數字")]
                public string Phone { get; set; } = string.Empty;

                [RequiredI18n]
                [EmailAddress(ErrorMessage = "信箱格式不正確")]
                public string Email { get; set; } = string.Empty;
            }

            private bool IsRoomRequired()
            {
                return UsageType == 1;
            }
        }
        /// <summary>
        /// 新增（舊系統）
        /// </summary>
        public Guid Insert(InsertVM vm)
        {
            // ===== 1. 處理資料轉換 =====

            // 如果前端送的是 AttendeeIds（User），轉換為 User 陣列
            if (!vm.User.Any() && vm.Room.Any())
            {
                // 假設發起人就是主持人
                vm.User = new List<Guid> { service.UserClaimsService.Me()?.Id ?? Guid.Empty };
                vm.Host = vm.User.FirstOrDefault();
            }

            // ===== 2. 驗證資料 =====
            if (db.Conference.WhereNotDeleted().Any(x => x.Name == vm.Name && x.StartTime == vm.StartTime))
            {
                throw new HttpException(I18nMessgae.DataExists);
            }

            // ===== 3. 計算開始和結束時間 =====
            var StartTime = vm.StartNow 
                ? DateTime.UtcNow.AddSeconds(ConferenceSettings.DelayStartTime) 
                : vm.StartTime!.Value;
            
            var EndTime = StartTime
                .AddHours(vm.DurationHH!.Value)
                .AddMinutes(vm.DurationSS!.Value);

            // ===== 4. 檢查會議室是否被預約 =====
            var used = db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                // ✅ 修復：只檢查有 StartTime/EndTime 的會議
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

            // ===== 5. 建立會議記錄 =====
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
                Ecs = [.. db.Ecs.Where(x => vm.Ecs.Contains(x.Id))],
                ConferenceUser = GetUsers(vm.User, vm.Host, vm.Recorder),
                Department = [.. db.SysDepartment.Where(x => vm.Department.Contains(x.Id))],
                CreateBy = userId!.Value,
                CreateAt = DateTime.UtcNow,
            };

            data.Status = GetStatus(vm.StartNow, data);
            data.ConferenceWebex = service.WebexMeetingService.Create(data);

            db.Conference.Add(data);
            db.SaveChanges();

            // ===== 6. 處理訪客 =====
            if (userId.HasValue)
            {
                var allVisitorIds = ProcessVisitors(userId.Value, vm.GuestsManual, vm.Guests);

                foreach (var visitorId in allVisitorIds)
                {
                    db.ConferenceVisitor.Add(new ConferenceVisitor
                    {
                        ConferenceId = conferenceId,
                        VisitorId = visitorId,
                    });
                }

                db.SaveChanges();
            }

            // ===== 7. 記錄（可包含費用資訊） =====
            var logMessage = $"{data.Name}({data.Id}) " +
                            $"RoomCost:{vm.RoomCost} EquipmentCost:{vm.EquipmentCost} " +
                            $"BoothCost:{vm.BoothCost} Total:{vm.TotalAmount} " +
                            $"PaymentMethod:{vm.PaymentMethod}";

            service.ConferenceMail.New(data);
            _ = service.LogServices.LogAsync("會議新增", logMessage);
            
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

            var StartTime = vm.StartNow ? DateTime.UtcNow.AddSeconds(ConferenceSettings.DelayStartTime) : vm.StartTime!.Value;
            var EndTime = StartTime.AddHours(vm.DurationHH!.Value).AddMinutes(vm.DurationSS!.Value);
            var used = db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                // ✅ 修復：只檢查有 StartTime/EndTime 的會議
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
                //.Include(x => x.ConferenceWebex)
                .Include(x => x.Room)
                .Include(x => x.Ecs)
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
            data.Ecs = [.. db.Ecs.Where(x => vm.Ecs.Contains(x.Id))];
            data.ConferenceUser = GetUsers(vm.User, vm.Host, vm.Recorder);
            data.Department = [.. db.SysDepartment.Where(x => vm.Department.Contains(x.Id))];
            data.Status = GetStatus(vm.StartNow, data);
            data.ConferenceWebex = service.WebexMeetingService.Create(data);
            //if (webex != null)
            //{
            //    data.Webex = webex;
            //}

            db.SaveChanges();

            var userId = service.UserClaimsService.Me()?.Id;
            if (userId.HasValue)
            {
                var oldVisitors = db.ConferenceVisitor
                    .Where(x => x.ConferenceId == vm.Id)
                    .ToList();
                db.ConferenceVisitor.RemoveRange(oldVisitors);

                var allVisitorIds = ProcessVisitors(userId.Value, vm.GuestsManual, vm.Guests);

                foreach (var visitorId in allVisitorIds)
                {
                    db.ConferenceVisitor.Add(new ConferenceVisitor
                    {
                        ConferenceId = vm.Id!.Value,
                        VisitorId = visitorId,
                    });
                }

                db.SaveChanges();
            }

            service.ConferenceMail.New(data, "[會議修改通知]");
            _ = _ = service.LogServices.LogAsync("會議編輯", $"{data.Name}({data.Id})");
        }

        // ============================================================
        // ✨ 【預約系統新增方法】
        // ============================================================

        /// <summary>
        /// 建立預約（待審核狀態）
        /// </summary>
        public Guid CreateReservation(InsertVM vm)
        {
            // ===== 驗證基本資料 =====
            if (string.IsNullOrWhiteSpace(vm.Name))
                throw new HttpException("會議名稱不能為空");
            
            if (vm.RoomId == null || !vm.RoomId.HasValue)
                throw new HttpException("必須選擇會議室");
            
            if (vm.SlotKeys == null || vm.SlotKeys.Count == 0)
                throw new HttpException("必須選擇至少一個時段");
            
            if (string.IsNullOrWhiteSpace(vm.PaymentMethod))
                throw new HttpException("必須選擇付款方式");

            // ===== 直接查詢時段並鎖定 =====
            var roomId = vm.RoomId.Value;
            
            // 根據時間字串找時段
            var slots = db.ConferenceRoomSlot
                .Where(s => s.RoomId == roomId && s.SlotStatus == 0)
                .AsEnumerable()
                .Where(s => vm.SlotKeys.Any(key => 
                    $"{s.StartTime:hh\\:mm\\:ss}-{s.EndTime:hh\\:mm\\:ss}" == key
                ))
                .ToList();

            // ===== 建立會議 =====
            var conferenceId = Guid.NewGuid();
            var userId = service.UserClaimsService.Me()?.Id 
                ?? throw new HttpException("無法取得使用者資訊");

            var conference = new Conference
            {
                Id = conferenceId,
                Name = vm.Name,
                UsageType = vm.UsageType ?? 1,
                MCU = null,
                Recording = false,
                Description = vm.Description,
                StartTime = null,
                EndTime = null,
                DurationHH = vm.DurationHH ?? 0,
                DurationSS = vm.DurationSS ?? 0,
                RRule = null,
                Status = 1,
                ReservationStatus = 1,  // 待審核
                ReviewedAt = null,
                ReviewedBy = null,
                ApprovedAt = null,
                PaymentDeadline = null,
                PaymentMethod = vm.PaymentMethod,
                PaymentStatus = 1,
                PaidAt = null,
                DepartmentCode = vm.DepartmentCode,
                RoomCost = (int)(vm.RoomCost ?? 0),
                EquipmentCost = (int)(vm.EquipmentCost ?? 0),
                BoothCost = (int)(vm.BoothCost ?? 0),
                TotalAmount = (int)(vm.TotalAmount ?? 0),
                CreateBy = userId,
                CreateAt = DateTime.UtcNow,
                Email = null,
                ConferenceUser = new List<ConferenceUser>
                {
                    new ConferenceUser
                    {
                        UserId = userId,
                        IsHost = true,
                        IsAttendees = true,
                        IsRecorder = false
                    }
                }
            };

            db.Conference.Add(conference);
            db.SaveChanges();

            // ===== 鎖定時段 =====
            foreach (var slot in slots)
            {
                slot.ConferenceId = conferenceId;
                slot.SlotStatus = 1;  // 審核中
                slot.LockedAt = DateTime.UtcNow;
            }
            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約系統", 
                $"預約建立 - {conference.Name} ({conference.Id})");

            return conferenceId;
        }

        /// <summary>
        /// 審核通過預約
        /// </summary>
        public void ApproveReservation(Guid conferenceId, Guid reviewedBy)
        {
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                .FirstOrDefault(c => c.Id == conferenceId && !c.DeleteAt.HasValue)
                ?? throw new HttpException("會議記錄不存在");

            // 檢查狀態
            if (conference.ReservationStatus != 1)
            {
                throw new HttpException($"只能審核狀態為「待審核」的預約");
            }

            // 更新會議狀態
            conference.ReservationStatus = 2;  // 2 = 待繳費
            conference.ReviewedAt = DateTime.UtcNow;
            conference.ReviewedBy = reviewedBy;
            conference.PaymentDeadline = DateTime.UtcNow.AddDays(7);  // 7 天內要繳費

            // 更新時段狀態（從審核中 → 預約成功）
            foreach (var slot in conference.ConferenceRoomSlots)
            {
                slot.SlotStatus = 2;  // 2 = 預約成功
            }

            db.SaveChanges();

            // 發送郵件通知
            _ = service.LogServices.LogAsync("預約審核", 
                $"審核通過 - {conference.Name} ({conference.Id})");
        }

        /// <summary>
        /// 拒絕預約
        /// </summary>
        public void RejectReservation(Guid conferenceId, string reason = "")
        {
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                .FirstOrDefault(c => c.Id == conferenceId && !c.DeleteAt.HasValue)
                ?? throw new HttpException("會議記錄不存在");

            // 只能拒絕「待審核」或「待繳費」的預約
            if (conference.ReservationStatus != 1 && conference.ReservationStatus != 2)
            {
                throw new HttpException($"無法拒絕此狀態的預約");
            }

            // 更新會議狀態
            conference.ReservationStatus = 0;  // 0 = 已釋放
            conference.ReviewedAt = DateTime.UtcNow;

            // 釋放所有時段
            foreach (var slot in conference.ConferenceRoomSlots)
            {
                slot.SlotStatus = 3;  // 3 = 已釋放
                slot.ConferenceId = null;
                slot.ReleasedAt = DateTime.UtcNow;
            }

            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約拒絕", 
                $"拒絕預約 - {conference.Name} ({conference.Id}) 原因: {reason}");
        }

        /// <summary>
        /// 確認繳費
        /// </summary>
        public void ConfirmPayment(Guid conferenceId)
        {
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                .Include(c => c.ConferenceUser)
                .FirstOrDefault(c => c.Id == conferenceId && !c.DeleteAt.HasValue)
                ?? throw new HttpException("會議記錄不存在");

            // 檢查狀態
            if (conference.ReservationStatus != 2)
            {
                throw new HttpException($"只能確認「待繳費」狀態的預約");
            }

            // ===== 1. 更新預約狀態 =====
            conference.ReservationStatus = 3;  // 3 = 預約成功
            conference.ApprovedAt = DateTime.UtcNow;
            conference.PaidAt = DateTime.UtcNow;
            conference.Status = 1;  // 會議狀態：未開始

            // ===== 2. 計算並設定 StartTime/EndTime（從 ConferenceRoomSlot） =====
            var slots = conference.ConferenceRoomSlots.OrderBy(s => s.SlotDate).ThenBy(s => s.StartTime).ToList();
            
            if (slots.Any())
            {
                // 第一個時段的開始時間
                var firstSlot = slots.First();
                conference.StartTime = firstSlot.SlotDate.ToDateTime(firstSlot.StartTime);

                // 最後一個時段的結束時間
                var lastSlot = slots.Last();
                conference.EndTime = lastSlot.SlotDate.ToDateTime(lastSlot.EndTime);
            }

            db.SaveChanges();

            // ===== 3. 建立 Webex 會議（如果需要） =====
            if (conference.MCU == 7)
            {
                conference.ConferenceWebex = service.WebexMeetingService.Create(conference);
                db.SaveChanges();
            }

            // ===== 4. 記錄 =====
            _ = service.LogServices.LogAsync("預約確認", 
                $"繳費確認 - {conference.Name} ({conference.Id}) 時段數: {slots.Count}");
        }

        // ============================================================
        // 【輔助方法】
        // ============================================================

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
        /// ⚠️ 修復：處理 StartTime/EndTime 為 NULL 的情況
        /// </summary>
        public byte GetStatus(bool startNow, Conference conference)
        {
            // ✅ 防呆：如果 StartTime/EndTime 為 NULL，返回待審核狀態
            if (conference.StartTime == null || conference.EndTime == null)
            {
                return 1;  // 1 = 待審核/待繳費狀態
            }

            // ✅ 使用 .Value 因為上面已經檢查 != null
            if (DateTime.UtcNow > (conference.FinishTime?.ToUniversalTime() ?? conference.EndTime!.Value.ToUniversalTime()))
            {
                return 4;  // 已結束
            }
            if (startNow || DateTime.UtcNow > conference.StartTime!.Value.ToUniversalTime())
            {
                service.JobService.DoEcs(conference);
                return 3;  // 進行中
            }
            if (DateTime.UtcNow.AddMinutes(ConferenceSettings.BeforeStart) > conference.StartTime!.Value.ToUniversalTime())
            {
                service.JobService.DoEcs(conference);
                return 2;  // 準備中
            }
            return 1;  // 未開始
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
                data.FinishTime = DateTime.UtcNow;
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
                data.DeleteAt = DateTime.UtcNow;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("會議刪除", $"{data.Name}({data.Id})");
            }
        }

        private List<Guid> ProcessVisitors(
                    Guid createdBy,
                    List<GuestManualVM> guestsManual,
                    List<Guid> visitorIds)
        {
            var allVisitorIds = new List<Guid>(visitorIds);

            foreach (var guest in guestsManual)
            {
                if (string.IsNullOrWhiteSpace(guest.CName) || string.IsNullOrWhiteSpace(guest.Email))
                    continue;

                var existingVisitor = db.Visitor
                    .WhereNotDeleted()
                    .FirstOrDefault(v => v.Email == guest.Email);

                Guid visitorId;

                if (existingVisitor != null)
                {
                    visitorId = existingVisitor.Id;
                }
                else
                {
                    visitorId = Guid.NewGuid();
                    var newVisitor = new Visitor
                    {
                        Id = visitorId,
                        CName = guest.CName,
                        Email = guest.Email,
                        CompanyName = guest.CompanyName,
                        Phone = guest.Phone,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = createdBy,
                    };
                    db.Visitor.Add(newVisitor);
                }

                if (!allVisitorIds.Contains(visitorId))
                {
                    allVisitorIds.Add(visitorId);
                }
            }

            return allVisitorIds;
        }

    }
}
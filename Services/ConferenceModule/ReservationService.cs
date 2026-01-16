using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using TASA.Program.ModelState;
using static TASA.Services.ConferenceModule.ConferenceService;

namespace TASA.Services.ConferenceModule
{
    public class ReservationService(TASAContext db, ServiceWrapper service) : IService
    {
        public class ApproveVM
        {
            public Guid ConferenceId { get; set; }
            public int? DiscountAmount { get; set; }
            public string? DiscountReason { get; set; }
        }

        public class RejectVM
        {
            public Guid ConferenceId { get; set; }
            public string? Reason { get; set; }
        }

        public class ReservationListVM
        {
            public Guid Id { get; set; }
            public string BookingNo { get; set; }
            public string ApplicantName { get; set; }
            public string Date { get; set; }
            public string Time { get; set; }
            public string RoomName { get; set; }
            public int TotalAmount { get; set; }
            public string Status { get; set; }
            public string PaymentStatusText { get; set; }
            public string? PaymentDeadline { get; set; }
            public string? PaymentMethod { get; set; }
            public string? DepartmentCode { get; set; }
            public string? RejectReason { get; set; }
            public string? UploadTime { get; set; }
            public List<SlotDetailVM> Slots { get; set; } = new();
        }

        public class SlotDetailVM
        {
            public Guid Id { get; set; }
            public string SlotDate { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public int SlotStatus { get; set; }
        }

        public class UpdateReservationVM
        {
            public Guid Id { get; set; }
            public string? PaymentStatus { get; set; }
        }

        /// <summary>
        /// ✅ 所有預約列表 - 支援彈性篩選
        /// </summary>
        public IQueryable<ReservationListVM> AllList(ReservationQueryVM query)
        {
            var queryable = db.Conference
                .AsNoTracking()
                .WhereNotDeleted();

            // 如果有 UserId,只查該使用者的預約
            if (query.UserId.HasValue)
            {
                queryable = queryable.Where(x => x.CreateBy == query.UserId.Value);
            }

            // 如果有指定 ReservationStatus,才篩選
            if (query.ReservationStatus.HasValue)
            {
                queryable = queryable.Where(x => x.ReservationStatus == query.ReservationStatus.Value);
            }

            return queryable
                .WhereIf(query.Keyword, x =>
                    x.Name.Contains(query.Keyword!) ||
                    x.CreateByNavigation.Name.Contains(query.Keyword!))
                .OrderBy(x =>
                    x.ReservationStatus == 2 ? 0 :  // 待繳費 (優先)
                    x.ReservationStatus == 1 ? 1 :  // 待審核
                    x.ReservationStatus == 3 ? 2 :  // 預約成功
                    x.ReservationStatus == 4 ? 3 :  // 審核拒絕
                    x.ReservationStatus == 0 ? 4 :  // 已釋放
                    5)                               // 未知
                .ThenByDescending(x => x.CreateAt)
                .Select(x => new ReservationListVM()
                {
                    Id = x.Id,
                    BookingNo = x.Id.ToString().Substring(0, 8),
                    ApplicantName = x.CreateByNavigation.Name,

                    Date = x.ConferenceRoomSlots.Any()
                            ? x.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd")
                            : "-",

                    Time = x.ConferenceRoomSlots.Any()
                            ? $"{x.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ " +
                              $"{x.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm} " +
                              $"({x.ConferenceRoomSlots.Count()} 個時段)"
                            : "-",

                    RoomName = x.ConferenceRoomSlots
                            .Select(s => s.Room.Name)
                            .FirstOrDefault() ?? "-",

                    TotalAmount = x.TotalAmount,

                    // ✅ 審核狀態
                    Status = x.ReservationStatus == 0 ? "已釋放" :
                        x.ReservationStatus == 1 ? "待審核" :
                        x.ReservationStatus == 2 ? "待繳費" :
                        x.ReservationStatus == 3 ? "預約成功" :
                        x.ReservationStatus == 4 ? "審核拒絕" : "未知",

                    // ✅ 付款狀態 (改名:待查帳)
                    PaymentStatusText = x.PaymentStatus == 1 ? "未付款" :
                                   x.PaymentStatus == 2 ? "待查帳" :
                                   x.PaymentStatus == 3 ? "已收款(全額)" :
                                   x.PaymentStatus == 4 ? "已收款(訂金30%)" :
                                   x.PaymentStatus == 5 ? "已收款(尾款70%)" : "未知",

                    PaymentDeadline = x.PaymentDeadline.HasValue
                        ? x.PaymentDeadline.Value.ToString("yyyy/MM/dd")
                        : null,

                    PaymentMethod = x.PaymentMethod,
                    DepartmentCode = x.DepartmentCode,
                    RejectReason = x.RejectReason,

                    Slots = x.ConferenceRoomSlots
                            .OrderBy(s => s.SlotDate)
                            .ThenBy(s => s.StartTime)
                            .Select(s => new SlotDetailVM
                            {
                                Id = s.Id,
                                SlotDate = s.SlotDate.ToString("yyyy/MM/dd"),
                                StartTime = s.StartTime.ToString(@"HH\:mm"),
                                EndTime = s.EndTime.ToString(@"HH\:mm"),
                                SlotStatus = s.SlotStatus
                            }).ToList()
                });
        }

        /// <summary>
        /// ✅ 待查帳列表 - 已上傳憑證但未確認的預約
        /// </summary>
        public IQueryable<ReservationListVM> PendingCheckList(BaseQueryVM query)
        {
            return db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x => x.ReservationStatus == 2      // 待繳費
                         && x.PaymentStatus == 2)          // 待查帳(已上傳憑證)
                .WhereIf(query.Keyword, x =>
                    x.Name.Contains(query.Keyword!) ||
                    x.CreateByNavigation.Name.Contains(query.Keyword!))
                .OrderByDescending(x => x.CreateAt)
                .Select(x => new ReservationListVM()
                {
                    Id = x.Id,
                    BookingNo = x.Id.ToString().Substring(0, 8),
                    ApplicantName = x.CreateByNavigation.Name,

                    Date = x.ConferenceRoomSlots.Any()
                        ? x.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd")
                        : "-",

                    Time = x.ConferenceRoomSlots.Any()
                        ? $"{x.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ " +
                          $"{x.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm} " +
                          $"({x.ConferenceRoomSlots.Count()} 個時段)"
                        : "-",

                    RoomName = x.ConferenceRoomSlots
                        .Select(s => s.Room.Name)
                        .FirstOrDefault() ?? "-",

                    TotalAmount = x.TotalAmount,
                    PaymentMethod = x.PaymentMethod,

                    Status = "待繳費",
                    PaymentStatusText = "待查帳",

                    // ✅ 上傳時間 (可以從 PaymentProof 表取得,這裡暫時用 CreateAt)
                    UploadTime = x.CreateAt.ToString("yyyy/MM/dd HH:mm"),

                    Slots = x.ConferenceRoomSlots
                        .OrderBy(s => s.SlotDate)
                        .ThenBy(s => s.StartTime)
                        .Select(s => new SlotDetailVM
                        {
                            Id = s.Id,
                            SlotDate = s.SlotDate.ToString("yyyy/MM/dd"),
                            StartTime = s.StartTime.ToString(@"HH\:mm"),
                            EndTime = s.EndTime.ToString(@"HH\:mm"),
                            SlotStatus = s.SlotStatus
                        }).ToList()
                });
        }

        /// <summary>
        /// 建立預約（待審核狀態）
        /// </summary>
        public Guid CreateReservation(InsertVM vm)
        {
            // ===== 1. 驗證基本資料 =====
            if (string.IsNullOrWhiteSpace(vm.Name))
                throw new HttpException("會議名稱不能為空");

            if (vm.RoomId == null || !vm.RoomId.HasValue)
                throw new HttpException("必須選擇會議室");

            if (vm.SlotKeys == null || vm.SlotKeys.Count == 0)
                throw new HttpException("必須選擇至少一個時段");

            if (string.IsNullOrWhiteSpace(vm.PaymentMethod))
                throw new HttpException("必須選擇付款方式");

            if (!vm.ReservationDate.HasValue)
                throw new HttpException("必須指定預約日期");

            // ===== 2. 解析時段 =====
            var roomId = vm.RoomId.Value;
            var slotDate = vm.ReservationDate.Value.Date;
            var slotDateOnly = DateOnly.FromDateTime(slotDate);
            var requestedSlots = new List<(TimeOnly Start, TimeOnly End)>();

            foreach (var slotKey in vm.SlotKeys)
            {
                var parts = slotKey.Split('-');
                if (parts.Length != 2)
                    throw new HttpException($"時段格式錯誤: {slotKey}");

                var startStr = parts[0].Trim();
                var endStr = parts[1].Trim();

                if (!TimeOnly.TryParse(startStr, out var start) ||
                    !TimeOnly.TryParse(endStr, out var end))
                    throw new HttpException($"時段格式錯誤: {slotKey}");

                requestedSlots.Add((start, end));
            }

            // ===== 3. 檢查時段衝突 =====
            var occupiedSlots = db.ConferenceRoomSlot
                .Where(s => s.RoomId == roomId
                         && s.SlotDate == slotDateOnly
                         && (s.SlotStatus == 1 || s.SlotStatus == 2))
                .Select(s => new { s.StartTime, s.EndTime })
                .ToList();

            foreach (var requested in requestedSlots)
            {
                var hasConflict = occupiedSlots.Any(occupied =>
                    requested.Start < occupied.EndTime && requested.End > occupied.StartTime
                );

                if (hasConflict)
                    throw new HttpException($"該會議室在 {slotDate:yyyy/MM/dd} 的時段 {requested.Start:HH\\:mm} ~ {requested.End:HH\\:mm} 已被佔用");
            }

            // ===== 4. 建立會議記錄 =====
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
                ReservationStatus = 1,  // ✅ 待審核
                ReviewedAt = null,
                ReviewedBy = null,
                ApprovedAt = null,
                PaymentDeadline = null,
                PaymentMethod = vm.PaymentMethod,
                PaymentStatus = 1,  // ✅ 未付款
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

            // ===== 5. 建立時段記錄 =====
            foreach (var (start, end) in requestedSlots)
            {
                var slot = new ConferenceRoomSlot
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = conferenceId,
                    RoomId = roomId,
                    SlotDate = slotDateOnly,
                    StartTime = start,
                    EndTime = end,
                    Price = 0,
                    PricingType = PricingType.Period,
                    SlotStatus = 1,  // 鎖定中
                    LockedAt = DateTime.UtcNow
                };

                db.ConferenceRoomSlot.Add(slot);
            }

            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約系統",
                $"預約建立 - {conference.Name} ({conference.Id})，日期: {slotDate:yyyy/MM/dd}，共 {requestedSlots.Count} 個時段");

            return conferenceId;
        }

        /// <summary>
        /// ✅ 審核通過
        /// </summary>
        public void ApproveReservation(ApproveVM vm, Guid reviewedBy)
        {
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                .FirstOrDefault(x => x.Id == vm.ConferenceId && !x.DeleteAt.HasValue)
                ?? throw new HttpException("會議不存在");

            if (conference.ReservationStatus != 1)
                throw new HttpException("該預約不在待審核狀態");

            var deadlineDays = GetPaymentDeadlineDays();

            // ✅ 狀態變更: 待審核 → 待繳費
            conference.ReservationStatus = 2;
            conference.ReviewedAt = DateTime.UtcNow;
            conference.ReviewedBy = reviewedBy;
            conference.PaymentDeadline = DateTime.UtcNow.AddDays(deadlineDays);

            // ✅ 折扣處理
            if (vm.DiscountAmount.HasValue && vm.DiscountAmount > 0)
            {
                conference.TotalAmount = Math.Max(0, conference.TotalAmount - vm.DiscountAmount.Value);
            }

            // ✅ 時段狀態變更: 鎖定中 → 已預約
            foreach (var slot in conference.ConferenceRoomSlots)
            {
                slot.SlotStatus = 2;
            }

            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約審核",
                $"審核通過 - {conference.Name} ({conference.Id}), 折扣: {vm.DiscountAmount ?? 0}");
        }

        /// <summary>
        /// ✅ 審核拒絕
        /// </summary>
        public void RejectReservation(RejectVM vm, Guid reviewedBy)
        {
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                .FirstOrDefault(x => x.Id == vm.ConferenceId && !x.DeleteAt.HasValue)
                ?? throw new HttpException("會議不存在");

            if (conference.ReservationStatus != 1)
                throw new HttpException("該預約不在待審核狀態");

            // ✅ 狀態變更: 待審核 → 審核拒絕
            conference.ReservationStatus = 4;
            conference.ReviewedAt = DateTime.UtcNow;
            conference.ReviewedBy = reviewedBy;
            conference.RejectReason = vm.Reason ?? "";

            // ✅ 時段釋放
            foreach (var slot in conference.ConferenceRoomSlots)
            {
                slot.SlotStatus = 3;  // 已釋放
                slot.ReleasedAt = DateTime.UtcNow;
            }

            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約拒絕",
                $"拒絕預約 - {conference.Name} ({conference.Id}) 原因: {vm.Reason}");
        }

        /// <summary>
        /// ✅ 確認收款 (查帳通過)
        /// </summary>
        public void ConfirmPayment(Guid conferenceId)
        {
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                .Include(c => c.ConferenceUser)
                .FirstOrDefault(c => c.Id == conferenceId && !c.DeleteAt.HasValue)
                ?? throw new HttpException("會議記錄不存在");

            if (conference.ReservationStatus != 2)
                throw new HttpException("只能確認「待繳費」狀態的預約");

            // ✅ 狀態變更: 待繳費 → 預約成功
            conference.ReservationStatus = 3;
            conference.ApprovedAt = DateTime.UtcNow;
            conference.PaidAt = DateTime.UtcNow;
            conference.Status = 1;

            // ✅ 設定會議開始/結束時間
            var slots = conference.ConferenceRoomSlots
                .OrderBy(s => s.SlotDate)
                .ThenBy(s => s.StartTime)
                .ToList();

            if (slots.Any())
            {
                var firstSlot = slots.First();
                conference.StartTime = firstSlot.SlotDate.ToDateTime(firstSlot.StartTime);

                var lastSlot = slots.Last();
                conference.EndTime = lastSlot.SlotDate.ToDateTime(lastSlot.EndTime);
            }

            db.SaveChanges();

            // ✅ 如果是 Webex 會議,建立會議室
            if (conference.MCU == 7)
            {
                conference.ConferenceWebex = service.WebexMeetingService.Create(conference);
                db.SaveChanges();
            }

            _ = service.LogServices.LogAsync("預約確認",
                $"繳費確認 - {conference.Name} ({conference.Id}) 時段數: {slots.Count}");
        }

        /// <summary>
        /// ✅ 更新付款狀態
        /// </summary>
        public void UpdateReservation(UpdateReservationVM vm)
        {
            var conference = db.Conference
                .FirstOrDefault(x => x.Id == vm.Id && !x.DeleteAt.HasValue)
                ?? throw new HttpException("會議不存在");

            // ✅ 檢查審核狀態 - 只有「待繳費」或「預約成功」才能修改付款狀態
            if (!string.IsNullOrEmpty(vm.PaymentStatus))
            {
                if (conference.ReservationStatus != 2 && conference.ReservationStatus != 3)
                {
                    var statusText = conference.ReservationStatus switch
                    {
                        0 => "已釋放",
                        1 => "待審核",
                        4 => "審核拒絕",
                        _ => "未知狀態"
                    };

                    throw new HttpException($"此預約目前為「{statusText}」,無法修改付款狀態");
                }

                // ✅ 更新付款狀態
                conference.PaymentStatus = vm.PaymentStatus switch
                {
                    "未付款" => 1,
                    "待查帳" => 2,
                    "已收款(全額)" => 3,
                    "已收款(訂金30%)" => 4,
                    "已收款(尾款70%)" => 5,
                    _ => conference.PaymentStatus
                };
            }

            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約更新",
                $"更新預約付款狀態 - {conference.Name} ({conference.Id})");
        }

        private int GetPaymentDeadlineDays()
        {
            var deadlineDays = db.SysConfig
                .Where(x => x.ConfigKey == "PAYMENT_DEADLINE_DAYS")
                .Select(x => x.ConfigValue)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(deadlineDays) && int.TryParse(deadlineDays, out var days))
                return days;

            return 7; // 預設 7 天
        }
    }
}
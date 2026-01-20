using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using TASA.Program.ModelState;
using static TASA.Services.ConferenceModule.ConferenceService;
using TASA.Models.Enums;

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
            public string ConferenceName { get; set; }
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
            public string? PaymentRejectReason { get; set; }
            public string? UploadTime { get; set; }

            public string? PaymentType { get; set; }        // "匯款" 或 "臨櫃"
            public string? LastFiveDigits { get; set; }     // 轉帳末五碼
            public int? TransferAmount { get; set; }        // 轉帳金額
            public string? TransferAt { get; set; }         // 轉帳時間
            public string? FilePath { get; set; }           // 檔案路徑
            public string? FileName { get; set; }           // 檔案名稱
            public string? Note { get; set; }

            public List<SlotDetailVM> Slots { get; set; } = new();
        }

        public class SlotDetailVM
        {
            public Guid Id { get; set; }
            public string SlotDate { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public SlotStatus SlotStatus { get; set; }  // ✅ 改成 SlotStatus enum
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
                .OrderByDescending(x => x.UpdateAt ?? x.CreateAt)
                .ThenByDescending(x => x.CreateAt)
                // ✅ 先取得 Conference 和最新的付款憑證
                .Select(x => new
                {
                    Conference = x,
                    LatestProof = x.ConferencePaymentProofs
                        .Where(p => p.DeleteAt == null)
                        .OrderByDescending(p => p.UploadedAt)
                        .FirstOrDefault()
                })
                // ✅ 再映射到 ViewModel
                .Select(x => new ReservationListVM()
                {
                    Id = x.Conference.Id,
                    BookingNo = x.Conference.Id.ToString().Substring(0, 8),
                    ApplicantName = x.Conference.CreateByNavigation.Name,
                    ConferenceName = x.Conference.Name,

                    Date = x.Conference.ConferenceRoomSlots.Any()
                            ? x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd")
                            : "-",

                    Time = x.Conference.ConferenceRoomSlots.Any()
                            ? $"{x.Conference.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ " +
                              $"{x.Conference.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm} " +
                              $"({x.Conference.ConferenceRoomSlots.Count()} 個時段)"
                            : "-",

                    RoomName = x.Conference.ConferenceRoomSlots
                            .Select(s => s.Room.Name)
                            .FirstOrDefault() ?? "-",

                    TotalAmount = x.Conference.TotalAmount,

                    // ✅ 審核狀態
                    Status = x.Conference.ReservationStatus == ReservationStatus.Cancelled ? "已取消" :
                             x.Conference.ReservationStatus == ReservationStatus.PendingApproval ? "待審核" :
                             x.Conference.ReservationStatus == ReservationStatus.PendingPayment ? "待繳費" :
                             x.Conference.ReservationStatus == ReservationStatus.Confirmed ? "預約成功" :
                             x.Conference.ReservationStatus == ReservationStatus.Rejected ? "審核拒絕" : "未知",

                    // ✅ 付款狀態
                    PaymentStatusText = x.Conference.PaymentStatus == PaymentStatus.Unpaid ? "未付款" :
                                       x.Conference.PaymentStatus == PaymentStatus.PendingVerification ? "待查帳" :
                                       x.Conference.PaymentStatus == PaymentStatus.Paid ? "已收款" :
                                       x.Conference.PaymentStatus == PaymentStatus.PendingReupload ? "待重新上傳" : "未知",

                    PaymentDeadline = x.Conference.PaymentDeadline.HasValue
                        ? x.Conference.PaymentDeadline.Value.ToString("yyyy/MM/dd")
                        : null,

                    PaymentMethod = x.Conference.PaymentMethod,
                    DepartmentCode = x.Conference.DepartmentCode,

                    // ✅ 審核拒絕原因 (來自 Conference 表 - 主管拒絕租借)
                    RejectReason = x.Conference.RejectReason,

                    // ✅ 付款拒絕原因 (來自 ConferencePaymentProof 表 - 會計退回憑證)
                    PaymentRejectReason = x.LatestProof != null ? x.LatestProof.RejectReason : null,

                    UploadTime = null,
                    PaymentType = null,
                    LastFiveDigits = null,
                    TransferAmount = null,
                    TransferAt = null,
                    FilePath = null,
                    FileName = null,
                    Note = null,

                    Slots = x.Conference.ConferenceRoomSlots
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
        public IQueryable<ReservationListVM> PendingCheckList(ReservationQueryVM query)
        {
            var queryable = db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .OrderByDescending(x => x.UpdateAt ?? x.CreateAt)
                .Where(x => x.ReservationStatus == ReservationStatus.PendingPayment);  // ✅ 固定查詢「待繳費」

            // ✅ 如果有指定付款狀態,才篩選
            if (query.PaymentStatus.HasValue)
            {
                queryable = queryable.Where(x => x.PaymentStatus == query.PaymentStatus.Value);
            }

            return queryable
                .WhereIf(query.Keyword, x =>
                    x.Name.Contains(query.Keyword!) ||
                    x.CreateByNavigation.Name.Contains(query.Keyword!))
                .OrderByDescending(x => x.CreateAt)
                .Select(x => new
                {
                    Conference = x,
                    LatestProof = x.ConferencePaymentProofs
                        .Where(p => p.DeleteAt == null && p.Status == ProofStatus.PendingReview)

                        .OrderByDescending(p => p.UploadedAt)
                        .FirstOrDefault()
                })
                .Select(x => new ReservationListVM()
                {
                    Id = x.Conference.Id,
                    BookingNo = x.Conference.Id.ToString().Substring(0, 8),
                    ApplicantName = x.Conference.CreateByNavigation.Name,

                    Date = x.Conference.ConferenceRoomSlots.Any()
                        ? x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd")
                        : "-",

                    Time = x.Conference.ConferenceRoomSlots.Any()
                        ? $"{x.Conference.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ " +
                          $"{x.Conference.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm} " +
                          $"({x.Conference.ConferenceRoomSlots.Count()} 個時段)"
                        : "-",

                    RoomName = x.Conference.ConferenceRoomSlots
                        .Select(s => s.Room.Name)
                        .FirstOrDefault() ?? "-",

                    TotalAmount = x.Conference.TotalAmount,
                    PaymentMethod = x.Conference.PaymentMethod,

                    Status = "待繳費",

                    // ✅ 動態顯示付款狀態
                    PaymentStatusText = x.Conference.PaymentStatus == PaymentStatus.Unpaid ? "未付款" :
   x.Conference.PaymentStatus == PaymentStatus.PendingVerification ? "待查帳" :
   x.Conference.PaymentStatus == PaymentStatus.Paid ? "已收款" :
   x.Conference.PaymentStatus == PaymentStatus.PendingReupload ? "待重新上傳" :
   "未知",

                    UploadTime = x.LatestProof != null
                        ? x.LatestProof.UploadedAt.ToString("yyyy/MM/dd HH:mm")
                        : null,

                    PaymentType = x.LatestProof != null ? x.LatestProof.PaymentType : null,
                    LastFiveDigits = x.LatestProof != null ? x.LatestProof.LastFiveDigits : null,
                    TransferAmount = x.LatestProof != null ? x.LatestProof.TransferAmount : null,
                    TransferAt = x.LatestProof != null && x.LatestProof.TransferAt.HasValue
                        ? x.LatestProof.TransferAt.Value.ToString("yyyy/MM/dd HH:mm")
                        : null,
                    FilePath = x.LatestProof != null ? x.LatestProof.FilePath : null,
                    FileName = x.LatestProof != null ? x.LatestProof.FileName : null,
                    Note = x.LatestProof != null ? x.LatestProof.Note : null,

                    Slots = x.Conference.ConferenceRoomSlots
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
                         && (s.SlotStatus == SlotStatus.Locked || s.SlotStatus == SlotStatus.Reserved))

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
                ReservationStatus = ReservationStatus.PendingApproval,  // ✅ 待審核

                ReviewedAt = null,
                ReviewedBy = null,
                ApprovedAt = null,
                PaymentDeadline = null,
                PaymentMethod = vm.PaymentMethod,
                PaymentStatus = PaymentStatus.Unpaid,  // ✅ 未付款
                PaidAt = null,
                DepartmentCode = vm.DepartmentCode,
                RoomCost = (int)(vm.RoomCost ?? 0),
                EquipmentCost = (int)(vm.EquipmentCost ?? 0),
                BoothCost = (int)(vm.BoothCost ?? 0),
                TotalAmount = (int)(vm.TotalAmount ?? 0),
                CreateBy = userId,
                CreateAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
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
                    SlotStatus = SlotStatus.Locked,  // 鎖定中
                    LockedAt = DateTime.UtcNow
                };

                db.ConferenceRoomSlot.Add(slot);
            }

            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約系統",
                $"預約建立 - {conference.Name} ({conference.Id})，日期: {slotDate:yyyy/MM/dd}，共 {requestedSlots.Count} 個時段");

            return conferenceId;
        }


        // public void AutoCancelOverdueReservations()
        // {
        //     var overdueReservations = db.Conference
        //         .Include(c => c.ConferenceRoomSlots)
        //         .Include(c => c.ConferencePaymentProofs)
        //         .Where(x => x.ReservationStatus == ReservationStatus.PendingPayment
        //                  && x.PaymentDeadline < DateTime.UtcNow
        //                  && x.DeleteAt == null)
        //         .ToList();

        //     foreach (var conference in overdueReservations)
        //     {
        //         // ✅ 檢查是否有上傳過憑證
        //         var hasProof = conference.ConferencePaymentProofs
        //             .Any(p => p.DeleteAt == null);

        //         conference.ReservationStatus = ReservationStatus.Cancelled;
        //         conference.CancelReason = "超過繳費期限";

        //         if (hasProof)
        //         {
        //             conference.HasPartialPayment = true;  // ✅ 標記為需要處理退款

        //             // TODO: 發送通知給總務
        //         }

        //         // 釋放會議室時段
        //         foreach (var slot in conference.ConferenceRoomSlots)
        //         {
        //             slot.SlotStatus = SlotStatus.Available;
        //             slot.ReleasedAt = DateTime.UtcNow;
        //         }
        //     }

        //     db.SaveChanges();
        // }

        /// <summary>
        /// ✅ 審核通過
        /// </summary>
        public void ApproveReservation(ApproveVM vm, Guid reviewedBy)
        {
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                .FirstOrDefault(x => x.Id == vm.ConferenceId && !x.DeleteAt.HasValue)
                ?? throw new HttpException("會議不存在");

            if (conference.ReservationStatus != ReservationStatus.PendingApproval)
                throw new HttpException("該預約不在待審核狀態");

            // ✅ 改用 SysConfigService 統一管理
            var deadlineDays = service.SysConfigService.GetPaymentDeadlineDays();

            // ✅ 狀態變更: 待審核 → 待繳費
            conference.ReservationStatus = ReservationStatus.PendingPayment;
            conference.ReviewedAt = DateTime.UtcNow;
            conference.ReviewedBy = reviewedBy;
            conference.PaymentDeadline = DateTime.UtcNow.AddDays(deadlineDays);
            conference.UpdateAt = DateTime.UtcNow;
            // ✅ 折扣處理
            if (vm.DiscountAmount.HasValue && vm.DiscountAmount > 0)
            {
                conference.TotalAmount = Math.Max(0, conference.TotalAmount - vm.DiscountAmount.Value);
            }

            // ✅ 時段狀態變更: 鎖定中 → 已預約
            foreach (var slot in conference.ConferenceRoomSlots)
            {
                slot.SlotStatus = SlotStatus.Reserved;
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

            if (conference.ReservationStatus != ReservationStatus.PendingApproval)
                throw new HttpException("該預約不在待審核狀態");

            // ✅ 狀態變更: 待審核 → 審核拒絕
            conference.ReservationStatus = ReservationStatus.Rejected;
            conference.ReviewedAt = DateTime.UtcNow;
            conference.ReviewedBy = reviewedBy;
            conference.RejectReason = vm.Reason ?? "";
            conference.UpdateAt = DateTime.UtcNow;
            // ✅ 時段釋放
            foreach (var slot in conference.ConferenceRoomSlots)
            {
                slot.SlotStatus = SlotStatus.Available;  // 已釋放
                slot.ReleasedAt = DateTime.UtcNow;
            }

            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約拒絕",
                $"拒絕預約 - {conference.Name} ({conference.Id}) 原因: {vm.Reason}");
        }
    }
}
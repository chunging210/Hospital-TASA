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

        public class GetReservationDetailDTO
        {
            public string ConferenceName { get; set; }
            public string Description { get; set; }
            public string ReservationDate { get; set; }
            public Guid DepartmentId { get; set; }
            public string Building { get; set; }
            public string Floor { get; set; }
            public Guid RoomId { get; set; }
            public string PaymentMethod { get; set; }
            public string DepartmentCode { get; set; }
            public List<string> SlotKeys { get; set; }
            public List<Guid> EquipmentIds { get; set; }
            public List<Guid> BoothIds { get; set; }

            public int RoomCost { get; set; }
            public int EquipmentCost { get; set; }
            public int BoothCost { get; set; }
            public int TotalAmount { get; set; }
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

            public string? PaymentType { get; set; }
            public string? LastFiveDigits { get; set; }
            public int? TransferAmount { get; set; }
            public string? TransferAt { get; set; }
            public string? FilePath { get; set; }
            public string? FileName { get; set; }
            public string? Note { get; set; }

            public List<SlotDetailVM> Slots { get; set; } = new();
        }

        public class SlotDetailVM
        {
            public Guid Id { get; set; }
            public string SlotDate { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public SlotStatus SlotStatus { get; set; }
        }

        public class CancelReservationVM
        {
            public Guid ReservationId { get; set; }
        }

        public class GetDetailVM
        {
            public string ReservationNo { get; set; }
        }


        public class DeleteReservationVM
        {
            public Guid ReservationId { get; set; }
        }

        /// <summary>
        /// ✅ 所有預約列表 - 支援彈性篩選
        /// </summary>
        public IQueryable<ReservationListVM> AllList(ReservationQueryVM query)
        {
            var queryable = db.Conference
                .AsNoTracking()
                .WhereNotDeleted();

            if (query.UserId.HasValue)
            {
                queryable = queryable.Where(x => x.CreateBy == query.UserId.Value);
            }

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
                .Select(x => new
                {
                    Conference = x,
                    LatestProof = x.ConferencePaymentProofs
                        .Where(p => p.DeleteAt == null)
                        .OrderByDescending(p => p.UploadedAt)
                        .FirstOrDefault()
                })
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

                    Status = x.Conference.ReservationStatus == ReservationStatus.Cancelled ? "已取消" :
                             x.Conference.ReservationStatus == ReservationStatus.PendingApproval ? "待審核" :
                             x.Conference.ReservationStatus == ReservationStatus.PendingPayment ? "待繳費" :
                             x.Conference.ReservationStatus == ReservationStatus.Confirmed ? "預約成功" :
                             x.Conference.ReservationStatus == ReservationStatus.Rejected ? "審核拒絕" : "未知",

                    PaymentStatusText = x.Conference.PaymentStatus == PaymentStatus.Unpaid ? "未付款" :
                                       x.Conference.PaymentStatus == PaymentStatus.PendingVerification ? "待查帳" :
                                       x.Conference.PaymentStatus == PaymentStatus.Paid ? "已收款" :
                                       x.Conference.PaymentStatus == PaymentStatus.PendingReupload ? "待重新上傳" : "未知",

                    PaymentDeadline = x.Conference.PaymentDeadline.HasValue
                        ? x.Conference.PaymentDeadline.Value.ToString("yyyy/MM/dd")
                        : null,

                    PaymentMethod = x.Conference.PaymentMethod,
                    DepartmentCode = x.Conference.DepartmentCode,
                    RejectReason = x.Conference.RejectReason,
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
                .Where(x => x.ReservationStatus == ReservationStatus.PendingPayment);

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

                    PaymentStatusText = x.Conference.PaymentStatus == PaymentStatus.Unpaid ? "未付款" :
                                       x.Conference.PaymentStatus == PaymentStatus.PendingVerification ? "待查帳" :
                                       x.Conference.PaymentStatus == PaymentStatus.Paid ? "已收款" :
                                       x.Conference.PaymentStatus == PaymentStatus.PendingReupload ? "待重新上傳" : "未知",

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
        /// ✅ 建立預約(待審核狀態)
        /// </summary>
        public Guid CreateReservation(InsertVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id
                ?? throw new HttpException("無法取得使用者資訊");

            // 驗證基本資料
            ValidateReservationData(vm);

            // 解析並檢查時段
            var (roomId, slotDate, slotDateOnly, requestedSlots) = ParseAndValidateSlots(vm, null);

            // 建立會議記錄
            var conferenceId = Guid.NewGuid();
            var conference = CreateConferenceEntity(conferenceId, vm, userId);

            db.Conference.Add(conference);
            db.SaveChanges();

            // 建立時段記錄
            CreateRoomSlots(conferenceId, roomId, slotDateOnly, requestedSlots);

            // 建立設備和攤位關聯
            CreateEquipmentLinks(conferenceId, vm.EquipmentIds, vm.BoothIds, slotDateOnly, requestedSlots);

            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約系統",
                $"預約建立 - {conference.Name} ({conference.Id})，日期: {slotDate:yyyy/MM/dd}，共 {requestedSlots.Count} 個時段");

            return conferenceId;
        }

        /// <summary>
        /// ✅ 更新預約
        /// </summary>
        public void UpdateReservation(InsertVM vm, Guid userId)
        {
            // 驗證 ReservationNo
            if (string.IsNullOrWhiteSpace(vm.ReservationNo))
                throw new HttpException("預約編號不能為空");

            Guid conferenceId;
            try
            {
                conferenceId = Guid.Parse(vm.ReservationNo);
            }
            catch
            {
                throw new HttpException("預約 ID 格式錯誤");
            }

            // 取得現有預約
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                .Include(c => c.ConferenceEquipments)
                .FirstOrDefault(x => x.Id == conferenceId && !x.DeleteAt.HasValue)
                ?? throw new HttpException("會議不存在");

            // 權限檢查
            if (conference.CreateBy != userId)
                throw new HttpException("您沒有權限修改此預約");

            // 狀態檢查
            if (conference.ReservationStatus != ReservationStatus.PendingApproval)
                throw new HttpException("只有「待審核」狀態的預約可以修改");

            // 驗證基本資料
            ValidateReservationData(vm);

            // 解析並檢查時段
            var (roomId, slotDate, slotDateOnly, requestedSlots) = ParseAndValidateSlots(vm, conferenceId);

            // 更新基本資訊
            UpdateConferenceEntity(conference, vm);

            // 刪除舊時段
            var oldSlots = conference.ConferenceRoomSlots.ToList();
            db.ConferenceRoomSlot.RemoveRange(oldSlots);

            // 建立新時段
            CreateRoomSlots(conferenceId, roomId, slotDateOnly, requestedSlots);

            // 刪除舊設備關聯
            var oldEquipments = conference.ConferenceEquipments.ToList();
            db.ConferenceEquipment.RemoveRange(oldEquipments);

            // 建立新設備和攤位關聯
            CreateEquipmentLinks(conferenceId, vm.EquipmentIds, vm.BoothIds, slotDateOnly, requestedSlots);

            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約更新",
                $"更新預約 - {conference.Name} ({conference.Id})");
        }

        /// <summary>
        /// ✅ 審核通過
        /// </summary>
        public void ApproveReservation(ApproveVM vm, Guid reviewedBy)
        {
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                .Include(c => c.ConferenceEquipments)
                .FirstOrDefault(x => x.Id == vm.ConferenceId && !x.DeleteAt.HasValue)
                ?? throw new HttpException("會議不存在");

            if (conference.ReservationStatus != ReservationStatus.PendingApproval)
                throw new HttpException("該預約不在待審核狀態");

            var deadlineDays = service.SysConfigService.GetPaymentDeadlineDays();

            conference.ReservationStatus = ReservationStatus.PendingPayment;
            conference.ReviewedAt = DateTime.UtcNow;
            conference.ReviewedBy = reviewedBy;
            conference.PaymentDeadline = DateTime.UtcNow.AddDays(deadlineDays);
            conference.UpdateAt = DateTime.UtcNow;

            if (vm.DiscountAmount.HasValue && vm.DiscountAmount > 0)
            {
                conference.TotalAmount = Math.Max(0, conference.TotalAmount - vm.DiscountAmount.Value);
            }

            foreach (var slot in conference.ConferenceRoomSlots)
            {
                slot.SlotStatus = SlotStatus.Reserved;
            }

            // ✅ 設備狀態變更: 鎖定中 → 已預約
            foreach (var equipment in conference.ConferenceEquipments)
            {
                equipment.EquipmentStatus = 2;  // 已預約
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
                .Include(c => c.ConferenceEquipments)
                .FirstOrDefault(x => x.Id == vm.ConferenceId && !x.DeleteAt.HasValue)
                ?? throw new HttpException("會議不存在");

            if (conference.ReservationStatus != ReservationStatus.PendingApproval)
                throw new HttpException("該預約不在待審核狀態");

            conference.ReservationStatus = ReservationStatus.Rejected;
            conference.ReviewedAt = DateTime.UtcNow;
            conference.ReviewedBy = reviewedBy;
            conference.RejectReason = vm.Reason ?? "";
            conference.UpdateAt = DateTime.UtcNow;

            foreach (var slot in conference.ConferenceRoomSlots)
            {
                slot.SlotStatus = SlotStatus.Available;
                slot.ReleasedAt = DateTime.UtcNow;
            }

            // ✅ 設備釋放
            foreach (var equipment in conference.ConferenceEquipments)
            {
                equipment.EquipmentStatus = 0;  // 可用
                equipment.ReleasedAt = DateTime.UtcNow;
            }


            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約拒絕",
                $"拒絕預約 - {conference.Name} ({conference.Id}) 原因: {vm.Reason}");
        }

        /// <summary>
        /// ✅ 取消預約
        /// </summary>
        public void CancelReservation(Guid conferenceId, Guid userId)
        {
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                .Include(c => c.ConferenceEquipments)
                .FirstOrDefault(x => x.Id == conferenceId && !x.DeleteAt.HasValue)
                ?? throw new HttpException("會議不存在");

            if (conference.CreateBy != userId)
                throw new HttpException("您沒有權限取消此預約");

            switch (conference.ReservationStatus)
            {
                case ReservationStatus.PendingApproval:
                case ReservationStatus.PendingPayment:
                    break;

                case ReservationStatus.Confirmed:
                    var earliestSlot = conference.ConferenceRoomSlots
                        .OrderBy(s => s.SlotDate)
                        .FirstOrDefault();

                    if (earliestSlot != null)
                    {
                        var daysUntilReservation = (earliestSlot.SlotDate.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days;

                        if (daysUntilReservation < 3)
                            throw new HttpException("距離使用不足 3 天,無法取消");
                    }
                    break;

                case ReservationStatus.Rejected:
                case ReservationStatus.Cancelled:
                default:
                    throw new HttpException("該預約目前狀態無法取消");
            }

            conference.ReservationStatus = ReservationStatus.Cancelled;
            conference.CancelledAt = DateTime.UtcNow;
            conference.CancelledBy = userId;
            conference.UpdateAt = DateTime.UtcNow;

            foreach (var slot in conference.ConferenceRoomSlots)
            {
                slot.SlotStatus = SlotStatus.Available;
                slot.ReleasedAt = DateTime.UtcNow;
            }

            foreach (var equipment in conference.ConferenceEquipments)
            {
                equipment.EquipmentStatus = 0;  // 可用
                equipment.ReleasedAt = DateTime.UtcNow;
            }


            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約取消",
                $"使用者取消預約 - {conference.Name} ({conference.Id})");
        }

        /// <summary>
        /// ✅ 刪除/移除預約 (軟刪除)
        /// </summary>
        public void DeleteReservation(Guid conferenceId, Guid userId)
        {
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)       // ✅ 包含時段
                .Include(c => c.ConferenceEquipments)      // ✅ 包含設備
                .FirstOrDefault(x => x.Id == conferenceId && !x.DeleteAt.HasValue)
                ?? throw new HttpException("會議不存在");

            if (conference.CreateBy != userId)
                throw new HttpException("您沒有權限移除此預約");

            if (conference.ReservationStatus != ReservationStatus.PendingApproval &&
                conference.ReservationStatus != ReservationStatus.Rejected)
            {
                throw new HttpException("只有「待審核」或「審核拒絕」的預約可以移除");
            }

            // ✅ 軟刪除會議本體
            conference.DeleteAt = DateTime.UtcNow;
            conference.UpdateAt = DateTime.UtcNow;

            // ✅ 如果是「待審核」狀態,需要釋放資源
            if (conference.ReservationStatus == ReservationStatus.PendingApproval)
            {
                // 1️⃣ 釋放會議室時段
                foreach (var slot in conference.ConferenceRoomSlots)
                {
                    slot.SlotStatus = SlotStatus.Available;
                    slot.ReleasedAt = DateTime.UtcNow;
                }

                // 2️⃣ 釋放設備和攤位
                foreach (var equipment in conference.ConferenceEquipments)
                {
                    equipment.EquipmentStatus = 0;  // 可用
                    equipment.ReleasedAt = DateTime.UtcNow;
                }
            }

            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約移除",
                $"使用者移除預約 - {conference.Name} ({conference.Id})");
        }
        /// <summary>
        /// ✅ 取得預約詳情
        /// </summary>
        public GetReservationDetailDTO GetReservationDetail(string reservationNo, Guid userId)
        {
            Guid conferenceId;

            try
            {
                conferenceId = Guid.Parse(reservationNo);
            }
            catch
            {
                throw new HttpException("預約 ID 格式錯誤");
            }

            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                    .ThenInclude(s => s.Room)
                .Include(c => c.ConferenceEquipments)
                .Where(c => c.Id == conferenceId && !c.DeleteAt.HasValue)
                .FirstOrDefault();

            if (conference == null)
                throw new HttpException("找不到預約");

            if (conference.CreateBy != userId)
                throw new HttpException("您沒有權限查看此預約");

            var slotKeys = conference.ConferenceRoomSlots
                .OrderBy(s => s.SlotDate)
                .ThenBy(s => s.StartTime)
                .Select(s => $"{s.StartTime:HH\\:mm\\:ss}-{s.EndTime:HH\\:mm\\:ss}")
                .ToList();

            var equipmentIds = conference.ConferenceEquipments
                .Where(e => e.EquipmentType != "9")
                .Select(e => e.EquipmentId)
                .ToList();

            var boothIds = conference.ConferenceEquipments
                .Where(e => e.EquipmentType == "9")
                .Select(e => e.EquipmentId)
                .ToList();

            var firstRoom = conference.ConferenceRoomSlots.FirstOrDefault()?.Room;

            return new GetReservationDetailDTO
            {
                ConferenceName = conference.Name,
                Description = conference.Description,
                ReservationDate = conference.ConferenceRoomSlots.Any()
                    ? conference.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy-MM-dd")
                    : DateTime.Now.ToString("yyyy-MM-dd"),
                DepartmentId = firstRoom?.DepartmentId ?? Guid.Empty,
                Building = firstRoom?.Building ?? "",
                Floor = firstRoom?.Floor ?? "",
                RoomId = conference.ConferenceRoomSlots.FirstOrDefault()?.RoomId ?? Guid.Empty,
                PaymentMethod = conference.PaymentMethod,
                DepartmentCode = conference.DepartmentCode,
                SlotKeys = slotKeys,
                EquipmentIds = equipmentIds,
                BoothIds = boothIds,
                RoomCost = conference.RoomCost,
                EquipmentCost = conference.EquipmentCost,
                BoothCost = conference.BoothCost,
                TotalAmount = conference.TotalAmount
            };
        }

        // ========== 以下是共用方法 ==========

        /// <summary>
        /// 驗證預約基本資料
        /// </summary>
        private void ValidateReservationData(InsertVM vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Name))
                throw new HttpException("會議名稱不能為空");

            if (!vm.RoomId.HasValue)
                throw new HttpException("必須選擇會議室");

            if (vm.SlotKeys == null || vm.SlotKeys.Count == 0)
                throw new HttpException("必須選擇至少一個時段");

            if (string.IsNullOrWhiteSpace(vm.PaymentMethod))
                throw new HttpException("必須選擇付款方式");

            if (!vm.ReservationDate.HasValue)
                throw new HttpException("必須指定預約日期");
        }

        /// <summary>
        /// 解析時段並檢查衝突
        /// </summary>
        private (Guid RoomId, DateTime SlotDate, DateOnly SlotDateOnly, List<(TimeOnly Start, TimeOnly End)> RequestedSlots)
            ParseAndValidateSlots(InsertVM vm, Guid? excludeConferenceId)
        {
            var roomId = vm.RoomId!.Value;
            var slotDate = vm.ReservationDate!.Value.Date;
            var slotDateOnly = DateOnly.FromDateTime(slotDate);
            var requestedSlots = new List<(TimeOnly Start, TimeOnly End)>();

            // 解析時段
            foreach (var slotKey in vm.SlotKeys!)
            {
                var parts = slotKey.Split('-');
                if (parts.Length != 2)
                    throw new HttpException($"時段格式錯誤: {slotKey}");

                if (!TimeOnly.TryParse(parts[0].Trim(), out var start) ||
                    !TimeOnly.TryParse(parts[1].Trim(), out var end))
                    throw new HttpException($"時段格式錯誤: {slotKey}");

                requestedSlots.Add((start, end));
            }

            // 檢查時段衝突
            var query = db.ConferenceRoomSlot
                .Where(s => s.RoomId == roomId
                         && s.SlotDate == slotDateOnly
                         && (s.SlotStatus == SlotStatus.Locked || s.SlotStatus == SlotStatus.Reserved));

            if (excludeConferenceId.HasValue)
            {
                query = query.Where(s => s.ConferenceId != excludeConferenceId.Value);
            }

            var occupiedSlots = query
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

            return (roomId, slotDate, slotDateOnly, requestedSlots);
        }

        /// <summary>
        /// 建立 Conference 實體(新增用)
        /// </summary>
        private Conference CreateConferenceEntity(Guid conferenceId, InsertVM vm, Guid userId)
        {
            return new Conference
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
                ReservationStatus = ReservationStatus.PendingApproval,
                ReviewedAt = null,
                ReviewedBy = null,
                ApprovedAt = null,
                PaymentDeadline = null,
                PaymentMethod = vm.PaymentMethod,
                PaymentStatus = PaymentStatus.Unpaid,
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
        }

        /// <summary>
        /// 更新 Conference 實體(更新用)
        /// </summary>
        private void UpdateConferenceEntity(Conference conference, InsertVM vm)
        {
            conference.Name = vm.Name;
            conference.Description = vm.Description;
            conference.PaymentMethod = vm.PaymentMethod;
            conference.DepartmentCode = vm.DepartmentCode;
            conference.RoomCost = (int)(vm.RoomCost ?? 0);
            conference.EquipmentCost = (int)(vm.EquipmentCost ?? 0);
            conference.BoothCost = (int)(vm.BoothCost ?? 0);
            conference.TotalAmount = (int)(vm.TotalAmount ?? 0);
            conference.DurationHH = vm.DurationHH ?? 0;
            conference.DurationSS = vm.DurationSS ?? 0;
            conference.UpdateAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 建立會議室時段記錄
        /// </summary>
        private void CreateRoomSlots(Guid conferenceId, Guid roomId, DateOnly slotDate, List<(TimeOnly Start, TimeOnly End)> requestedSlots)
        {
            // ✅ 查詢該會議室的所有時段價格設定
            var roomSlotPrices = db.SysRoomPricePeriod
                .Where(rs => rs.RoomId == roomId && rs.IsEnabled)
                .ToList();

            foreach (var (start, end) in requestedSlots)
            {

                var startTimeSpan = start.ToTimeSpan();
                var endTimeSpan = end.ToTimeSpan();

                // ✅ 找到對應的時段價格
                var priceInfo = roomSlotPrices.FirstOrDefault(rs =>
                    rs.StartTime == startTimeSpan && rs.EndTime == endTimeSpan
                );

                var slot = new ConferenceRoomSlot
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = conferenceId,
                    RoomId = roomId,
                    SlotDate = slotDate,
                    StartTime = start,
                    EndTime = end,
                    Price = priceInfo?.Price ?? 0,  // ✅ 使用實際價格(沒找到就是 0)
                    PricingType = PricingType.Period,  // ✅ 時段計價
                    SlotStatus = SlotStatus.Locked,
                    LockedAt = DateTime.UtcNow
                };

                db.ConferenceRoomSlot.Add(slot);
            }
        }

        /// <summary>
        /// 建立設備和攤位關聯
        /// </summary>
        private void CreateEquipmentLinks(Guid conferenceId, List<Guid>? equipmentIds, List<Guid>? boothIds, DateOnly slotDate, List<(TimeOnly Start, TimeOnly End)> requestedSlots)
        {
            // ✅ 收集所有需要的設備 ID
            var allEquipmentIds = new List<Guid>();
            if (equipmentIds != null) allEquipmentIds.AddRange(equipmentIds);
            if (boothIds != null) allEquipmentIds.AddRange(boothIds);

            if (!allEquipmentIds.Any()) return;

            // ✅ 一次查詢所有設備資料
            var equipmentDict = db.Equipment
                .Where(e => allEquipmentIds.Contains(e.Id))
                .ToDictionary(e => e.Id, e => e);

            // ✅ 對每個時段都要建立設備關聯
            foreach (var (start, end) in requestedSlots)
            {
                // 新增設備 (Type = 8)
                if (equipmentIds != null && equipmentIds.Any())
                {
                    foreach (var equipmentId in equipmentIds)
                    {
                        if (!equipmentDict.TryGetValue(equipmentId, out var equipment))
                            continue;

                        db.ConferenceEquipment.Add(new ConferenceEquipment
                        {
                            Id = Guid.NewGuid(),
                            ConferenceId = conferenceId,
                            EquipmentId = equipmentId,
                            EquipmentName = equipment.Name,
                            EquipmentPrice = equipment.RentalPrice,
                            EquipmentType = "8",
                            SlotDate = slotDate,  // ✅ 新增日期
                            StartTime = start,    // ✅ 新增開始時間
                            EndTime = end,        // ✅ 新增結束時間
                            EquipmentStatus = 1,  // ✅ 鎖定中
                            LockedAt = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // 新增攤位 (Type = 9)
                if (boothIds != null && boothIds.Any())
                {
                    foreach (var boothId in boothIds)
                    {
                        if (!equipmentDict.TryGetValue(boothId, out var booth))
                            continue;

                        db.ConferenceEquipment.Add(new ConferenceEquipment
                        {
                            Id = Guid.NewGuid(),
                            ConferenceId = conferenceId,
                            EquipmentId = boothId,
                            EquipmentName = booth.Name,
                            EquipmentPrice = booth.RentalPrice,
                            EquipmentType = "9",
                            SlotDate = slotDate,  // ✅ 新增日期
                            StartTime = start,    // ✅ 新增開始時間
                            EndTime = end,        // ✅ 新增結束時間
                            EquipmentStatus = 1,  // ✅ 鎖定中
                            LockedAt = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }
    }
}
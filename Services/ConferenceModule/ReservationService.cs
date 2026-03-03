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
            public DateTime? PaymentDeadline { get; set; }  // 自訂繳費期限（沒填則用 DB 設定）
        }

        public class GetReservationDetailDTO
        {
            public string ConferenceName { get; set; }
            public string Description { get; set; }
            public int? ExpectedAttendees { get; set; }  // ✅ 預計到達人數
            public string OrganizerUnit { get; set; }
            public string Chairman { get; set; }
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
            public int ParkingTicketCount { get; set; }
            public int ParkingTicketCost { get; set; }
            public int TotalAmount { get; set; }
            public List<AttachmentDTO> Attachments { get; set; } = new();
            public class AttachmentDTO
            {
                public Guid Id { get; set; }
                public AttachmentType Type { get; set; }
                public string FileName { get; set; } = string.Empty;
                public string FilePath { get; set; } = string.Empty;
                public long FileSize { get; set; }
            }
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
            public string OrganizerUnit { get; set; }
            public string Chairman { get; set; }
            public string Date { get; set; }
            public string Time { get; set; }
            public string RoomName { get; set; }
            public int TotalAmount { get; set; }
            public int ParkingTicketCount { get; set; }  // ✅ 停車券張數
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

            // ✅ 多階層審核欄位
            public int CurrentApprovalLevel { get; set; }
            public int TotalApprovalLevels { get; set; }
            public string? CurrentApproverName { get; set; }  // 當前審核人名稱

            // ✅ 預計到達人數
            public int? ExpectedAttendees { get; set; }

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

        /// <summary>
        /// 預約總覽詳情 DTO（用於查看詳情，非編輯）
        /// </summary>
        public class ReservationDetailViewVM
        {
            public Guid Id { get; set; }
            public string BookingNo { get; set; }
            public string ApplicantName { get; set; }
            public string ConferenceName { get; set; }
            public string? Description { get; set; }  // 會議內容
            public int? ExpectedAttendees { get; set; }  // ✅ 預計到達人數
            public string OrganizerUnit { get; set; }
            public string Chairman { get; set; }
            public string Date { get; set; }
            public string Time { get; set; }
            public string RoomName { get; set; }
            public int TotalAmount { get; set; }
            public int ParkingTicketCount { get; set; }  // ✅ 停車券張數
            public string Status { get; set; }
            public string PaymentStatusText { get; set; }
            public string? PaymentDeadline { get; set; }
            public string? PaymentMethod { get; set; }
            public string? DepartmentCode { get; set; }
            public string? RejectReason { get; set; }
            public string? PaymentRejectReason { get; set; }

            // ✅ 折扣資訊
            public int? DiscountAmount { get; set; }
            public string? DiscountReason { get; set; }

            // 新增欄位
            public List<string> Equipments { get; set; } = new();  // 加租設備名稱列表
            public List<string> Booths { get; set; } = new();  // 攤位加租名稱列表
            public List<AttachmentViewVM> Attachments { get; set; } = new();  // 附件列表

            // ✅ 審核歷程
            public List<ApprovalHistoryVM> ApprovalHistory { get; set; } = new();
        }

        public class ApprovalHistoryVM
        {
            public int Level { get; set; }
            public string ApproverName { get; set; } = string.Empty;
            public string? ApprovedByName { get; set; }  // 實際審核人（可能是代理人）
            public string Status { get; set; } = string.Empty;  // Pending, Approved, Rejected
            public string StatusText { get; set; } = string.Empty;  // 待審核, 已通過, 已拒絕, 決行跳過
            public string? ApprovedAt { get; set; }
            public string? Reason { get; set; }  // 拒絕原因或決行標記
            public int? DiscountAmount { get; set; }
            public string? DiscountReason { get; set; }
        }

        public class AttachmentViewVM
        {
            public Guid Id { get; set; }
            public string Type { get; set; }  // "agenda" 或 "document"
            public string TypeText { get; set; }  // "議程表" 或 "會議文件"
            public string FileName { get; set; }
            public string FilePath { get; set; }
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

            var userId = service.UserClaimsService.Me()?.Id;

            var queryable = db.Conference
                .AsNoTracking()
                .WhereNotDeleted();


            if (userId.HasValue)
    {
        var userPermissions = service.AuthRoleServices.GetUserPermissions(userId.Value);

        // ✅ Admin / Director / Accountant 可以看到所有預約
        var canViewAll = userPermissions.Roles.Any(r =>
            r == "ADMIN" || r == "ADMINN" || r == "DIRECTOR" || r == "ACCOUNTANT");

        // 如果使用者是會議室管理者，但不是可看全部的角色
        if (userPermissions.IsRoomManager && !canViewAll)
        {
            var managedRoomIds = userPermissions.ManagedRoomIds;

            // ✅ 只顯示該使用者管理的會議室的預約
            queryable = queryable.Where(c =>
                c.ConferenceRoomSlots.Any(s => managedRoomIds.Contains(s.RoomId))
            );
        }
    }


            if (query.UserId.HasValue)
            {
                queryable = queryable.Where(x => x.CreateBy == query.UserId.Value);
            }

            if (query.ReservationStatus.HasValue)
            {
                queryable = queryable.Where(x => x.ReservationStatus == query.ReservationStatus.Value);
            }

            // ✅ 篩選:付款狀態
            if (query.PaymentStatus.HasValue)
            {
                queryable = queryable.Where(x => x.PaymentStatus == query.PaymentStatus.Value);
            }

            return queryable
                .WhereIf(query.Keyword, x =>
                    x.Name.Contains(query.Keyword!) ||
                    x.CreateByNavigation.Name.Contains(query.Keyword!) ||
                    x.Id.ToString().StartsWith(query.Keyword!))
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
                    OrganizerUnit = x.Conference.OrganizerUnit,
                    Chairman = x.Conference.Chairman,

                    // ✅ 列表：只顯示日期範圍
                    Date = x.Conference.ConferenceRoomSlots.Any()
                            ? (x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate) == x.Conference.ConferenceRoomSlots.Max(s => s.SlotDate)
                                ? x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd")
                                : x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd") + " ~ " +
                                  x.Conference.ConferenceRoomSlots.Max(s => s.SlotDate).ToString("yyyy/MM/dd"))
                            : "-",

                    // ✅ 列表：單日顯示時間，跨日顯示「-」（詳細時間在 Detail 看）
                    Time = x.Conference.ConferenceRoomSlots.Any()
                            ? (x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate) == x.Conference.ConferenceRoomSlots.Max(s => s.SlotDate)
                                ? $"{x.Conference.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ " +
                                  $"{x.Conference.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm}"
                                : "-")
                            : "-",

                    RoomName = x.Conference.ConferenceRoomSlots
                            .Select(s => s.Room.Name)
                            .FirstOrDefault() ?? "-",

                    TotalAmount = x.Conference.TotalAmount,
                    ParkingTicketCount = x.Conference.ParkingTicketCount,  // ✅ 停車券張數

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

                    // ✅ 多階層審核欄位
                    CurrentApprovalLevel = x.Conference.CurrentApprovalLevel,
                    TotalApprovalLevels = x.Conference.TotalApprovalLevels,
                    CurrentApproverName = x.Conference.ApprovalHistory
                        .Where(h => h.Level == x.Conference.CurrentApprovalLevel + 1 && h.Status == ApprovalStatus.Pending)
                        .Select(h => h.Approver.Name)
                        .FirstOrDefault(),

                    // ✅ 預計到達人數
                    ExpectedAttendees = x.Conference.ExpectedAttendees,

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
        /// ✅ 我的審核列表 - 根據狀態顯示不同資料
        /// - Admin：可以查看所有預約
        /// - 一般審核人員：
        ///   - 待審核：顯示「輪到我審核」的預約
        ///   - 已核准/已拒絕：顯示「我曾經審核過」的預約
        /// </summary>
        public IQueryable<ReservationListVM> MyPendingApprovalList(Guid userId, ReservationQueryVM query)
        {
            // ✅ 取得今天日期，用於檢查代理人
            var today = DateOnly.FromDateTime(DateTime.Now);

            // ✅ 檢查是否為 Admin
            var isAdmin = service.AuthRoleServices.HasAnyRole(userId, "ADMIN", "ADMINN");

            var queryable = db.Conference
                .AsNoTracking()
                .WhereNotDeleted();

            // ✅ Admin 可以看到所有預約
            if (isAdmin)
            {
                // 根據狀態篩選（如果有選擇的話）
                if (query.ReservationStatus.HasValue)
                {
                    queryable = queryable.Where(c => c.ReservationStatus == query.ReservationStatus.Value);
                }
                // 全部狀態時不加篩選，顯示所有預約
            }
            // ✅ 一般審核人員：根據篩選狀態決定查詢邏輯
            else if (!query.ReservationStatus.HasValue)
            {
                // 全部狀態：查詢「輪到我審核的待審核」+「我曾經審核過的已核准/已拒絕」
                queryable = queryable
                    .Where(c =>
                        // 待審核且輪到我審核
                        (c.ReservationStatus == ReservationStatus.PendingApproval &&
                         c.ApprovalHistory.Any(h =>
                            h.Level == c.CurrentApprovalLevel + 1 &&
                            h.Status == ApprovalStatus.Pending &&
                            (
                                h.ApproverId == userId ||
                                db.RoomManagerDelegate.Any(d =>
                                    d.DelegateUserId == userId &&
                                    d.ManagerId == h.ApproverId &&
                                    d.IsEnabled &&
                                    d.DeleteAt == null &&
                                    d.StartDate <= today &&
                                    d.EndDate >= today
                                )
                            )
                         ))
                        ||
                        // 已核准/已拒絕且我曾經審核過
                        ((c.ReservationStatus == ReservationStatus.PendingPayment ||
                          c.ReservationStatus == ReservationStatus.Rejected ||
                          c.ReservationStatus == ReservationStatus.Confirmed) &&
                         c.ApprovalHistory.Any(h =>
                            h.Status != ApprovalStatus.Pending &&
                            (h.ApprovedBy == userId || h.ApproverId == userId)
                         ))
                    );
            }
            else if (query.ReservationStatus.Value == ReservationStatus.PendingApproval)
            {
                // 待審核：查詢「輪到我審核」或「我是代理人」的預約
                queryable = queryable
                    .Where(c => c.ReservationStatus == ReservationStatus.PendingApproval)
                    .Where(c => c.ApprovalHistory.Any(h =>
                        h.Level == c.CurrentApprovalLevel + 1 &&
                        h.Status == ApprovalStatus.Pending &&
                        (
                            h.ApproverId == userId ||  // 本人
                            db.RoomManagerDelegate.Any(d =>  // 代理人
                                d.DelegateUserId == userId &&
                                d.ManagerId == h.ApproverId &&
                                d.IsEnabled &&
                                d.DeleteAt == null &&
                                d.StartDate <= today &&
                                d.EndDate >= today
                            )
                        )
                    ));
            }
            else
            {
                // 已核准/已拒絕：查詢「我曾經審核過」的預約
                queryable = queryable
                    .Where(c => c.ReservationStatus == query.ReservationStatus.Value)
                    .Where(c => c.ApprovalHistory.Any(h =>
                        h.Status != ApprovalStatus.Pending &&  // 已審核過
                        (
                            h.ApprovedBy == userId ||  // 我審核的
                            h.ApproverId == userId  // 或我是原本的審核人
                        )
                    ));
            }

            return queryable
                .WhereIf(query.Keyword, x =>
                    x.Name.Contains(query.Keyword!) ||
                    x.CreateByNavigation.Name.Contains(query.Keyword!) ||
                    x.Id.ToString().StartsWith(query.Keyword!))
                .OrderByDescending(x => x.CreateAt)
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
                    OrganizerUnit = x.Conference.OrganizerUnit,
                    Chairman = x.Conference.Chairman,
                    Date = x.Conference.ConferenceRoomSlots.Any()
                            ? (x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate) == x.Conference.ConferenceRoomSlots.Max(s => s.SlotDate)
                                ? x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd")
                                : x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd") + " ~ " +
                                  x.Conference.ConferenceRoomSlots.Max(s => s.SlotDate).ToString("yyyy/MM/dd"))
                            : "-",
                    Time = x.Conference.ConferenceRoomSlots.Any()
                            ? (x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate) == x.Conference.ConferenceRoomSlots.Max(s => s.SlotDate)
                                ? $"{x.Conference.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ " +
                                  $"{x.Conference.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm}"
                                : "-")
                            : "-",
                    RoomName = x.Conference.ConferenceRoomSlots
                            .Select(s => s.Room.Name)
                            .FirstOrDefault() ?? "-",
                    TotalAmount = x.Conference.TotalAmount,
                    ParkingTicketCount = x.Conference.ParkingTicketCount,
                    Status = x.Conference.ReservationStatus == ReservationStatus.PendingApproval ? "待審核" :
                             x.Conference.ReservationStatus == ReservationStatus.PendingPayment ? "已核准" :
                             x.Conference.ReservationStatus == ReservationStatus.Rejected ? "已拒絕" :
                             x.Conference.ReservationStatus == ReservationStatus.Confirmed ? "已完成" : "未知",
                    PaymentStatusText = x.Conference.PaymentStatus == PaymentStatus.Unpaid ? "未付款" :
                                        x.Conference.PaymentStatus == PaymentStatus.PendingVerification ? "待查帳" :
                                        x.Conference.PaymentStatus == PaymentStatus.Paid ? "已收款" : "未付款",
                    PaymentDeadline = null,
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
                    // ✅ 多階層審核欄位
                    CurrentApprovalLevel = x.Conference.CurrentApprovalLevel,
                    TotalApprovalLevels = x.Conference.TotalApprovalLevels,
                    CurrentApproverName = x.Conference.ApprovalHistory
                        .Where(h => h.Level == x.Conference.CurrentApprovalLevel + 1 && h.Status == ApprovalStatus.Pending)
                        .Select(h => h.Approver.Name)
                        .FirstOrDefault(),

                    // ✅ 預計到達人數
                    ExpectedAttendees = x.Conference.ExpectedAttendees,

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
                    x.CreateByNavigation.Name.Contains(query.Keyword!) ||
                    x.Id.ToString().StartsWith(query.Keyword!))
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
                    ConferenceName = x.Conference.Name,
                    OrganizerUnit = x.Conference.OrganizerUnit,
                    Chairman = x.Conference.Chairman,

                    // ✅ 列表：只顯示日期範圍
                    Date = x.Conference.ConferenceRoomSlots.Any()
                        ? (x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate) == x.Conference.ConferenceRoomSlots.Max(s => s.SlotDate)
                            ? x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd")
                            : x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd") + " ~ " +
                              x.Conference.ConferenceRoomSlots.Max(s => s.SlotDate).ToString("yyyy/MM/dd"))
                        : "-",

                    // ✅ 列表：單日顯示時間，跨日顯示「-」（詳細時間在 Detail 看）
                    Time = x.Conference.ConferenceRoomSlots.Any()
                        ? (x.Conference.ConferenceRoomSlots.Min(s => s.SlotDate) == x.Conference.ConferenceRoomSlots.Max(s => s.SlotDate)
                            ? $"{x.Conference.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ " +
                              $"{x.Conference.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm}"
                            : "-")
                        : "-",

                    RoomName = x.Conference.ConferenceRoomSlots
                        .Select(s => s.Room.Name)
                        .FirstOrDefault() ?? "-",

                    TotalAmount = x.Conference.TotalAmount,
                    ParkingTicketCount = x.Conference.ParkingTicketCount,  // ✅ 停車券張數
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

            // 解析並檢查時段（支援多日）
            var (roomId, slotsByDate) = ParseAndValidateSlots(vm, null);

            // 建立會議記錄
            var conferenceId = Guid.NewGuid();
            var conference = CreateConferenceEntity(conferenceId, vm, userId);

            db.Conference.Add(conference);
            db.SaveChanges();

            // 建立時段記錄（支援多日）
            CreateRoomSlots(conferenceId, roomId, slotsByDate);

            // 建立設備和攤位關聯（支援多日）
            CreateEquipmentLinks(conferenceId, vm.EquipmentIds, vm.BoothIds, slotsByDate);

            if (vm.Attachments != null && vm.Attachments.Any())
            {
                SaveAttachments(conferenceId, vm.Attachments, userId);
            }

            db.SaveChanges();

            // 計算總時段數和日期範圍
            var totalSlots = slotsByDate.Values.Sum(s => s.Count);
            var dateRange = slotsByDate.Keys.OrderBy(d => d).ToList();
            var dateRangeStr = dateRange.Count == 1
                ? $"{dateRange.First():yyyy/MM/dd}"
                : $"{dateRange.First():yyyy/MM/dd} ~ {dateRange.Last():yyyy/MM/dd}";

            _ = service.LogServices.LogAsync("預約系統",
                $"預約建立 - {conference.Name} ({conference.Id})，日期: {dateRangeStr}，共 {totalSlots} 個時段");

            // 寄送預約通知信件
            service.ConferenceMail.ReservationCreated(conferenceId);

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

            // 解析並檢查時段（支援多日）
            var (roomId, slotsByDate) = ParseAndValidateSlots(vm, conferenceId);

            // 更新基本資訊
            UpdateConferenceEntity(conference, vm);

            // 刪除舊時段
            var oldSlots = conference.ConferenceRoomSlots.ToList();
            db.ConferenceRoomSlot.RemoveRange(oldSlots);

            // 建立新時段（支援多日）
            CreateRoomSlots(conferenceId, roomId, slotsByDate);

            // 刪除舊設備關聯
            var oldEquipments = conference.ConferenceEquipments.ToList();
            db.ConferenceEquipment.RemoveRange(oldEquipments);

            // 建立新設備和攤位關聯（支援多日）
            CreateEquipmentLinks(conferenceId, vm.EquipmentIds, vm.BoothIds, slotsByDate);

            // 軟刪除舊附件
            var oldAttachments = db.ConferenceAttachment
                .Where(a => a.ConferenceId == conferenceId && a.DeleteAt == null)
                .ToList();

            foreach (var att in oldAttachments)
            {
                att.DeleteAt = DateTime.Now;
            }

            if (vm.Attachments != null && vm.Attachments.Any())
            {
                SaveAttachments(conferenceId, vm.Attachments, userId);
            }


            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約更新",
                $"更新預約 - {conference.Name} ({conference.Id})");
        }

        /// <summary>
        /// ✅ 審核通過（多階段審核）
        /// </summary>
        public void ApproveReservation(ApproveVM vm, Guid reviewedBy)
        {
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                .Include(c => c.ConferenceEquipments)
                .Include(c => c.ApprovalHistory)
                .FirstOrDefault(x => x.Id == vm.ConferenceId && !x.DeleteAt.HasValue)
                ?? throw new HttpException("會議不存在");

            if (conference.ReservationStatus != ReservationStatus.PendingApproval)
                throw new HttpException("該預約不在待審核狀態");

            // ✅ 取得下一關待審核的紀錄
            var nextLevel = conference.CurrentApprovalLevel + 1;
            var currentApproval = conference.ApprovalHistory
                .FirstOrDefault(h => h.Level == nextLevel && h.Status == ApprovalStatus.Pending)
                ?? throw new HttpException("找不到待審核的關卡");

            // ✅ 檢查審核人權限（本人或代理人）
            var canApprove = currentApproval.ApproverId == reviewedBy
                          || IsActiveDelegate(reviewedBy, currentApproval.ApproverId);

            if (!canApprove)
                throw new HttpException("您沒有權限審核此關卡");

            // ✅ 更新該關卡狀態
            currentApproval.Status = ApprovalStatus.Approved;
            currentApproval.ApprovedAt = DateTime.Now;
            currentApproval.ApprovedBy = reviewedBy;
            currentApproval.DiscountAmount = vm.DiscountAmount;
            currentApproval.DiscountReason = vm.DiscountReason;
            currentApproval.PaymentDeadline = vm.PaymentDeadline;

            // ✅ 更新預約的當前審核層級
            conference.CurrentApprovalLevel = nextLevel;
            conference.UpdateAt = DateTime.Now;

            // ✅ 累積折扣（每關都可以設定，加總）
            if (vm.DiscountAmount.HasValue && vm.DiscountAmount > 0)
            {
                conference.DiscountAmount = (conference.DiscountAmount ?? 0) + vm.DiscountAmount.Value;
                conference.DiscountReason = string.IsNullOrEmpty(conference.DiscountReason)
                    ? vm.DiscountReason
                    : $"{conference.DiscountReason}; {vm.DiscountReason}";
                conference.TotalAmount = Math.Max(0, conference.TotalAmount - vm.DiscountAmount.Value);
            }

            var isLastLevel = nextLevel >= conference.TotalApprovalLevels;

            if (isLastLevel)
            {
                // ✅ 最後一關：完成審核，進入待繳費
                DateTime paymentDeadline;
                if (vm.PaymentDeadline.HasValue)
                {
                    if (vm.PaymentDeadline.Value.Date < DateTime.Today)
                        throw new HttpException("繳費期限不能小於今天");
                    paymentDeadline = vm.PaymentDeadline.Value;
                }
                else
                {
                    var deadlineDays = service.SysConfigService.GetPaymentDeadlineDays(conference.DepartmentId);
                    paymentDeadline = DateTime.Now.AddDays(deadlineDays);
                }

                conference.ReservationStatus = ReservationStatus.PendingPayment;
                conference.ReviewedAt = DateTime.Now;
                conference.ReviewedBy = reviewedBy;
                conference.PaymentDeadline = paymentDeadline;

                // 時段狀態變更
                foreach (var slot in conference.ConferenceRoomSlots)
                {
                    slot.SlotStatus = SlotStatus.Reserved;
                }

                // 設備狀態變更
                foreach (var equipment in conference.ConferenceEquipments)
                {
                    equipment.EquipmentStatus = 2;  // 已預約
                }

                db.SaveChanges();

                _ = service.LogServices.LogAsync("預約審核",
                    $"審核通過（全部 {conference.TotalApprovalLevels} 關）- {conference.Name} ({conference.Id}), 總折扣: {conference.DiscountAmount ?? 0}");

                // 寄送審核通過通知給申請人
                service.ConferenceMail.ReservationApproved(vm.ConferenceId, conference.DiscountAmount, conference.DiscountReason);
            }
            else
            {
                // ✅ 還有下一關：通知下一位審核人
                db.SaveChanges();

                _ = service.LogServices.LogAsync("預約審核",
                    $"第 {nextLevel}/{conference.TotalApprovalLevels} 關審核通過 - {conference.Name} ({conference.Id})");

                // 取得下一關審核人並寄信通知
                var nextApproval = conference.ApprovalHistory
                    .FirstOrDefault(h => h.Level == nextLevel + 1);

                if (nextApproval != null)
                {
                    service.ConferenceMail.NotifyNextApprover(vm.ConferenceId, nextApproval.ApproverId, nextLevel + 1, conference.TotalApprovalLevels);
                }
            }
        }

        /// <summary>
        /// ✅ 決行（直接通過所有剩餘關卡）
        /// </summary>
        public void FastTrackApproval(ApproveVM vm, Guid reviewedBy)
        {
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                .Include(c => c.ConferenceEquipments)
                .Include(c => c.ApprovalHistory)
                .FirstOrDefault(x => x.Id == vm.ConferenceId && !x.DeleteAt.HasValue)
                ?? throw new HttpException("會議不存在");

            if (conference.ReservationStatus != ReservationStatus.PendingApproval)
                throw new HttpException("該預約不在待審核狀態");

            // ✅ 取得當前待審核的關卡
            var nextLevel = conference.CurrentApprovalLevel + 1;
            var currentApproval = conference.ApprovalHistory
                .FirstOrDefault(h => h.Level == nextLevel && h.Status == ApprovalStatus.Pending)
                ?? throw new HttpException("找不到待審核的關卡");

            // ✅ 檢查審核人權限（本人或代理人）
            var canApprove = currentApproval.ApproverId == reviewedBy
                          || IsActiveDelegate(reviewedBy, currentApproval.ApproverId);

            if (!canApprove)
                throw new HttpException("您沒有權限審核此關卡");

            // ✅ 更新當前關卡狀態（標記為決行）
            currentApproval.Status = ApprovalStatus.Approved;
            currentApproval.ApprovedAt = DateTime.Now;
            currentApproval.ApprovedBy = reviewedBy;
            currentApproval.DiscountAmount = vm.DiscountAmount;
            currentApproval.DiscountReason = vm.DiscountReason;
            currentApproval.PaymentDeadline = vm.PaymentDeadline;
            currentApproval.Reason = "【決行】";  // 標記為決行

            // ✅ 將所有後續關卡也標記為通過（決行跳過）
            var remainingApprovals = conference.ApprovalHistory
                .Where(h => h.Level > nextLevel && h.Status == ApprovalStatus.Pending)
                .ToList();

            foreach (var approval in remainingApprovals)
            {
                approval.Status = ApprovalStatus.Approved;
                approval.ApprovedAt = DateTime.Now;
                approval.ApprovedBy = reviewedBy;
                approval.Reason = $"【決行跳過】由第 {nextLevel} 關決行";
            }

            // ✅ 累積折扣
            if (vm.DiscountAmount.HasValue && vm.DiscountAmount > 0)
            {
                conference.DiscountAmount = (conference.DiscountAmount ?? 0) + vm.DiscountAmount.Value;
                conference.DiscountReason = string.IsNullOrEmpty(conference.DiscountReason)
                    ? vm.DiscountReason
                    : $"{conference.DiscountReason}; {vm.DiscountReason}";
                conference.TotalAmount = Math.Max(0, conference.TotalAmount - vm.DiscountAmount.Value);
            }

            // ✅ 直接進入待繳費狀態
            DateTime paymentDeadline;
            if (vm.PaymentDeadline.HasValue)
            {
                if (vm.PaymentDeadline.Value.Date < DateTime.Today)
                    throw new HttpException("繳費期限不能小於今天");
                paymentDeadline = vm.PaymentDeadline.Value;
            }
            else
            {
                var deadlineDays = service.SysConfigService.GetPaymentDeadlineDays(conference.DepartmentId);
                paymentDeadline = DateTime.Now.AddDays(deadlineDays);
            }

            conference.CurrentApprovalLevel = conference.TotalApprovalLevels;  // 跳到最後
            conference.ReservationStatus = ReservationStatus.PendingPayment;
            conference.ReviewedAt = DateTime.Now;
            conference.ReviewedBy = reviewedBy;
            conference.PaymentDeadline = paymentDeadline;
            conference.UpdateAt = DateTime.Now;

            // 時段狀態變更
            foreach (var slot in conference.ConferenceRoomSlots)
            {
                slot.SlotStatus = SlotStatus.Reserved;
            }

            // 設備狀態變更
            foreach (var equipment in conference.ConferenceEquipments)
            {
                equipment.EquipmentStatus = 2;  // 已預約
            }

            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約決行",
                $"第 {nextLevel} 關決行通過（跳過剩餘 {remainingApprovals.Count} 關）- {conference.Name} ({conference.Id})");

            // 寄送審核通過通知給申請人
            service.ConferenceMail.ReservationApproved(vm.ConferenceId, conference.DiscountAmount, conference.DiscountReason);
        }

        /// <summary>
        /// ✅ 審核拒絕（多階段審核 - 任一關拒絕即結束）
        /// </summary>
        public void RejectReservation(RejectVM vm, Guid reviewedBy)
        {
            var conference = db.Conference
                .Include(c => c.ConferenceRoomSlots)
                .Include(c => c.ConferenceEquipments)
                .Include(c => c.ApprovalHistory)
                .FirstOrDefault(x => x.Id == vm.ConferenceId && !x.DeleteAt.HasValue)
                ?? throw new HttpException("會議不存在");

            if (conference.ReservationStatus != ReservationStatus.PendingApproval)
                throw new HttpException("該預約不在待審核狀態");

            // ✅ 取得當前待審核的關卡
            var nextLevel = conference.CurrentApprovalLevel + 1;
            var currentApproval = conference.ApprovalHistory
                .FirstOrDefault(h => h.Level == nextLevel && h.Status == ApprovalStatus.Pending)
                ?? throw new HttpException("找不到待審核的關卡");

            // ✅ 檢查審核人權限（本人或代理人）
            var canApprove = currentApproval.ApproverId == reviewedBy
                          || IsActiveDelegate(reviewedBy, currentApproval.ApproverId);

            if (!canApprove)
                throw new HttpException("您沒有權限審核此關卡");

            // ✅ 更新該關卡狀態
            currentApproval.Status = ApprovalStatus.Rejected;
            currentApproval.ApprovedAt = DateTime.Now;
            currentApproval.ApprovedBy = reviewedBy;
            currentApproval.Reason = vm.Reason;

            // ✅ 更新預約狀態
            conference.ReservationStatus = ReservationStatus.Rejected;
            conference.ReviewedAt = DateTime.Now;
            conference.ReviewedBy = reviewedBy;
            conference.RejectReason = vm.Reason ?? "";
            conference.UpdateAt = DateTime.Now;

            // 釋放時段
            foreach (var slot in conference.ConferenceRoomSlots)
            {
                slot.SlotStatus = SlotStatus.Available;
                slot.ReleasedAt = DateTime.Now;
            }

            // 釋放設備
            foreach (var equipment in conference.ConferenceEquipments)
            {
                equipment.EquipmentStatus = 0;  // 可用
                equipment.ReleasedAt = DateTime.Now;
            }

            db.SaveChanges();

            _ = service.LogServices.LogAsync("預約拒絕",
                $"第 {nextLevel}/{conference.TotalApprovalLevels} 關審核拒絕 - {conference.Name} ({conference.Id}) 原因: {vm.Reason}");

            // 寄送審核拒絕通知
            service.ConferenceMail.ReservationRejected(vm.ConferenceId, vm.Reason);
        }

        /// <summary>
        /// 檢查是否為有效的代理人
        /// </summary>
        private bool IsActiveDelegate(Guid userId, Guid managerId)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            return db.RoomManagerDelegate
                .AsNoTracking()
                .Any(d => d.DelegateUserId == userId
                       && d.ManagerId == managerId
                       && d.IsEnabled
                       && d.DeleteAt == null
                       && d.StartDate <= today
                       && d.EndDate >= today);
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

            int daysUntilReservation = 0;
            var earliestSlot = conference.ConferenceRoomSlots
                .OrderBy(s => s.SlotDate)
                .FirstOrDefault();

            if (earliestSlot != null)
            {
                daysUntilReservation = (earliestSlot.SlotDate.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days;
            }

            // 記錄是否已繳費（用於後續寄送退費通知）
            var hasPaid = conference.PaymentStatus == PaymentStatus.Paid;

            switch (conference.ReservationStatus)
            {
                case ReservationStatus.PendingApproval:
                case ReservationStatus.PendingPayment:
                    break;

                case ReservationStatus.Confirmed:
                    if (daysUntilReservation < 3)
                        throw new HttpException("距離使用不足 3 天,無法取消");
                    break;

                case ReservationStatus.Rejected:
                case ReservationStatus.Cancelled:
                default:
                    throw new HttpException("該預約目前狀態無法取消");
            }

            conference.ReservationStatus = ReservationStatus.Cancelled;
            conference.CancelledAt = DateTime.Now;
            conference.CancelledBy = userId;
            conference.UpdateAt = DateTime.Now;

            foreach (var slot in conference.ConferenceRoomSlots)
            {
                slot.SlotStatus = SlotStatus.Available;
                slot.ReleasedAt = DateTime.Now;
            }

            foreach (var equipment in conference.ConferenceEquipments)
            {
                equipment.EquipmentStatus = 0;  // 可用
                equipment.ReleasedAt = DateTime.Now;
            }


            db.SaveChanges();

            // 判斷是否為管理員取消（操作者 != 預約者）
            var isCancelledByAdmin = userId != conference.CreateBy;

            if (isCancelledByAdmin)
            {
                // 取得管理員名稱
                var admin = db.AuthUser.FirstOrDefault(u => u.Id == userId);
                var adminName = admin?.Name ?? "管理員";

                _ = service.LogServices.LogAsync("預約取消",
                    $"管理員取消預約 - {conference.Name} ({conference.Id})，操作者：{adminName}");

                // 通知預約者預約已被管理員取消
                service.ConferenceMail.ReservationCancelledByAdmin(conferenceId, adminName);
            }
            else
            {
                _ = service.LogServices.LogAsync("預約取消",
                    $"使用者取消預約 - {conference.Name} ({conference.Id})");
            }

            // 如果已繳費，通知總務退費
            if (hasPaid)
            {
                service.ConferenceMail.RefundNotify(conferenceId, daysUntilReservation);
            }
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
            conference.DeleteAt = DateTime.Now;
            conference.UpdateAt = DateTime.Now;

            // ✅ 如果是「待審核」狀態,需要釋放資源
            if (conference.ReservationStatus == ReservationStatus.PendingApproval)
            {
                // 1️⃣ 釋放會議室時段
                foreach (var slot in conference.ConferenceRoomSlots)
                {
                    slot.SlotStatus = SlotStatus.Available;
                    slot.ReleasedAt = DateTime.Now;
                }

                // 2️⃣ 釋放設備和攤位
                foreach (var equipment in conference.ConferenceEquipments)
                {
                    equipment.EquipmentStatus = 0;  // 可用
                    equipment.ReleasedAt = DateTime.Now;
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


            var attachments = db.ConferenceAttachment
                   .Where(a => a.ConferenceId == conferenceId && a.DeleteAt == null)
                   .Select(a => new GetReservationDetailDTO.AttachmentDTO
                   {
                       Id = a.Id,
                       Type = a.AttachmentType,
                       FileName = a.FileName,
                       FilePath = a.FilePath,
                       FileSize = a.FileSize ?? 0
                   })
                   .ToList();

            return new GetReservationDetailDTO
            {
                ConferenceName = conference.Name,
                Description = conference.Description,
                ExpectedAttendees = conference.ExpectedAttendees,  // ✅ 預計到達人數
                OrganizerUnit = conference.OrganizerUnit,
                Chairman = conference.Chairman,
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
                TotalAmount = conference.TotalAmount,
                Attachments = attachments
            };
        }

        /// <summary>
        /// 取得預約詳情（用於查看，非編輯）
        /// </summary>
        public ReservationDetailViewVM GetReservationDetailView(Guid conferenceId)
        {
            var conference = db.Conference
                .AsNoTracking()
                .Include(c => c.CreateByNavigation)
                .Include(c => c.ConferenceRoomSlots)
                    .ThenInclude(s => s.Room)
                .Include(c => c.ConferenceEquipments)
                .Include(c => c.ConferencePaymentProofs.Where(p => p.DeleteAt == null))
                .Include(c => c.ApprovalHistory)
                    .ThenInclude(h => h.Approver)
                .Include(c => c.ApprovalHistory)
                    .ThenInclude(h => h.ApprovedByUser)
                .Where(c => c.Id == conferenceId && !c.DeleteAt.HasValue)
                .FirstOrDefault();

            if (conference == null)
                throw new HttpException("找不到預約");

            // 取得設備名稱（排除攤位，攤位 EquipmentType = "9"）
            // 使用快照欄位 EquipmentName
            var equipments = conference.ConferenceEquipments
                .Where(e => e.EquipmentType != "9")
                .Select(e => e.EquipmentName)
                .Distinct()
                .ToList();

            // 取得攤位名稱
            var booths = conference.ConferenceEquipments
                .Where(e => e.EquipmentType == "9")
                .Select(e => e.EquipmentName)
                .Distinct()
                .ToList();

            // 取得附件
            var attachments = db.ConferenceAttachment
                .Where(a => a.ConferenceId == conferenceId && a.DeleteAt == null)
                .Select(a => new AttachmentViewVM
                {
                    Id = a.Id,
                    Type = a.AttachmentType == AttachmentType.Agenda ? "agenda" : "document",
                    TypeText = a.AttachmentType == AttachmentType.Agenda ? "議程表" : "會議文件",
                    FileName = a.FileName,
                    FilePath = a.FilePath
                })
                .ToList();

            // 取得最新的付款憑證拒絕原因
            var latestProof = conference.ConferencePaymentProofs
                .OrderByDescending(p => p.UploadedAt)
                .FirstOrDefault();

            return new ReservationDetailViewVM
            {
                Id = conference.Id,
                BookingNo = conference.Id.ToString().Substring(0, 8),
                ApplicantName = conference.CreateByNavigation?.Name ?? "-",
                ConferenceName = conference.Name,
                Description = conference.Description,
                ExpectedAttendees = conference.ExpectedAttendees,  // ✅ 預計到達人數
                OrganizerUnit = conference.OrganizerUnit,
                Chairman = conference.Chairman,
                // ✅ Detail：顯示完整起迄時間（含日期）
                Date = conference.ConferenceRoomSlots.Any()
                    ? (conference.ConferenceRoomSlots.Min(s => s.SlotDate) == conference.ConferenceRoomSlots.Max(s => s.SlotDate)
                        ? conference.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd")
                        : conference.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd") + " ~ " +
                          conference.ConferenceRoomSlots.Max(s => s.SlotDate).ToString("yyyy/MM/dd"))
                    : "-",
                // ✅ Detail：跨日顯示「起始日期時間 ~ 結束日期時間」
                Time = conference.ConferenceRoomSlots.Any()
                    ? (conference.ConferenceRoomSlots.Min(s => s.SlotDate) == conference.ConferenceRoomSlots.Max(s => s.SlotDate)
                        ? $"{conference.ConferenceRoomSlots.Min(s => s.StartTime):HH\\:mm} ~ " +
                          $"{conference.ConferenceRoomSlots.Max(s => s.EndTime):HH\\:mm}"
                        : $"{conference.ConferenceRoomSlots.Min(s => s.SlotDate):M/d} " +
                          $"{conference.ConferenceRoomSlots.OrderBy(s => s.SlotDate).ThenBy(s => s.StartTime).First().StartTime:HH\\:mm} ~ " +
                          $"{conference.ConferenceRoomSlots.Max(s => s.SlotDate):M/d} " +
                          $"{conference.ConferenceRoomSlots.OrderByDescending(s => s.SlotDate).ThenByDescending(s => s.EndTime).First().EndTime:HH\\:mm}")
                    : "-",
                // ✅ Detail：顯示完整地點（大樓 + 樓層 + 會議室名稱）
                RoomName = conference.ConferenceRoomSlots
                    .Select(s => s.Room != null
                        ? $"{s.Room.Building} {s.Room.Floor} 樓 {s.Room.Name}"
                        : "-")
                    .FirstOrDefault() ?? "-",
                TotalAmount = conference.TotalAmount,
                ParkingTicketCount = conference.ParkingTicketCount,  // ✅ 停車券張數
                Status = conference.ReservationStatus == ReservationStatus.Cancelled ? "已取消" :
                         conference.ReservationStatus == ReservationStatus.PendingApproval ? "待審核" :
                         conference.ReservationStatus == ReservationStatus.PendingPayment ? "待繳費" :
                         conference.ReservationStatus == ReservationStatus.Confirmed ? "預約成功" :
                         conference.ReservationStatus == ReservationStatus.Rejected ? "審核拒絕" : "未知",
                PaymentStatusText = conference.PaymentStatus == PaymentStatus.Unpaid ? "未付款" :
                                   conference.PaymentStatus == PaymentStatus.PendingVerification ? "待查帳" :
                                   conference.PaymentStatus == PaymentStatus.Paid ? "已收款" :
                                   conference.PaymentStatus == PaymentStatus.PendingReupload ? "待重新上傳" : "未知",
                PaymentDeadline = conference.PaymentDeadline.HasValue
                    ? conference.PaymentDeadline.Value.ToString("yyyy/MM/dd")
                    : null,
                PaymentMethod = conference.PaymentMethod,
                DepartmentCode = conference.DepartmentCode,
                RejectReason = conference.RejectReason,
                PaymentRejectReason = latestProof?.RejectReason,
                DiscountAmount = conference.DiscountAmount,      // ✅ 折扣金額
                DiscountReason = conference.DiscountReason,      // ✅ 折扣原因
                Equipments = equipments,
                Booths = booths,
                Attachments = attachments,
                // ✅ 審核歷程
                ApprovalHistory = conference.ApprovalHistory
                    .OrderBy(h => h.Level)
                    .Select(h => new ApprovalHistoryVM
                    {
                        Level = h.Level,
                        ApproverName = h.Approver?.Name ?? "-",
                        ApprovedByName = h.ApprovedByUser?.Name,
                        Status = h.Status.ToString(),
                        StatusText = h.Status == ApprovalStatus.Pending ? "待審核" :
                                    h.Status == ApprovalStatus.Approved ?
                                        (h.Reason?.Contains("決行跳過") == true ? "決行跳過" :
                                         h.Reason?.Contains("決行") == true ? "已通過【決行】" : "已通過") :
                                    h.Status == ApprovalStatus.Rejected ? "已拒絕" : "未知",
                        ApprovedAt = h.ApprovedAt?.ToString("yyyy/MM/dd HH:mm"),
                        Reason = h.Reason,
                        DiscountAmount = h.DiscountAmount,
                        DiscountReason = h.DiscountReason
                    })
                    .ToList()
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

            if ((vm.SlotKeys == null || vm.SlotKeys.Count == 0) &&
                (vm.SlotInfos == null || vm.SlotInfos.Count == 0))
                throw new HttpException("必須選擇至少一個時段");

            if (string.IsNullOrWhiteSpace(vm.PaymentMethod))
                throw new HttpException("必須選擇付款方式");

            // ✅ 判斷是跨日模式還是單日模式
            var isMultiDay = vm.StartDate.HasValue && vm.EndDate.HasValue;

            if (!isMultiDay && !vm.ReservationDate.HasValue)
                throw new HttpException("必須指定預約日期");

            // ✅ 根據會議室的分院 ID 取得最早預約天數設定
            var room = db.SysRoom.AsNoTracking().FirstOrDefault(r => r.Id == vm.RoomId);
            var departmentId = room?.DepartmentId;
            var minAdvanceDays = service.SysConfigService.GetMinAdvanceBookingDays(departmentId);
            var minDate = DateTime.Today.AddDays(minAdvanceDays);

            if (isMultiDay)
            {
                // 跨日模式：檢查開始日期
                if (vm.StartDate!.Value.Date < minDate)
                    throw new HttpException($"預約日期必須在 {minAdvanceDays} 天後（最早可選 {minDate:yyyy-MM-dd}）");

                if (vm.EndDate!.Value.Date < vm.StartDate.Value.Date)
                    throw new HttpException("結束日期不能早於開始日期");
            }
            else
            {
                // 單日模式：檢查預約日期
                if (vm.ReservationDate!.Value.Date < minDate)
                    throw new HttpException($"預約日期必須在 {minAdvanceDays} 天後（最早可選 {minDate:yyyy-MM-dd}）");
            }
        }

        /// <summary>
        /// 解析時段並檢查衝突（支援單日和跨日模式）
        /// </summary>
        private (Guid RoomId, Dictionary<DateOnly, List<(TimeOnly Start, TimeOnly End, bool IsSetup)>> SlotsByDate)
            ParseAndValidateSlots(InsertVM vm, Guid? excludeConferenceId)
        {
            var roomId = vm.RoomId!.Value;
            var slotsByDate = new Dictionary<DateOnly, List<(TimeOnly Start, TimeOnly End, bool IsSetup)>>();

            // ✅ 判斷是跨日模式還是單日模式
            var isMultiDay = vm.StartDate.HasValue && vm.EndDate.HasValue;

            // ✅ 優先使用新格式 SlotInfos（包含 isSetup 和 Date 資訊）
            if (vm.SlotInfos != null && vm.SlotInfos.Count > 0)
            {
                foreach (var slotInfo in vm.SlotInfos)
                {
                    var parts = slotInfo.Key.Split('-');
                    if (parts.Length != 2)
                        throw new HttpException($"時段格式錯誤: {slotInfo.Key}");

                    if (!TimeOnly.TryParse(parts[0].Trim(), out var start) ||
                        !TimeOnly.TryParse(parts[1].Trim(), out var end))
                        throw new HttpException($"時段格式錯誤: {slotInfo.Key}");

                    // ✅ 解析日期（跨日模式必須有 Date，單日模式用 ReservationDate）
                    DateOnly slotDateOnly;
                    if (!string.IsNullOrEmpty(slotInfo.Date))
                    {
                        if (!DateOnly.TryParse(slotInfo.Date, out slotDateOnly))
                            throw new HttpException($"日期格式錯誤: {slotInfo.Date}");
                    }
                    else if (vm.ReservationDate.HasValue)
                    {
                        slotDateOnly = DateOnly.FromDateTime(vm.ReservationDate.Value.Date);
                    }
                    else
                    {
                        throw new HttpException("時段缺少日期資訊");
                    }

                    if (!slotsByDate.ContainsKey(slotDateOnly))
                        slotsByDate[slotDateOnly] = new List<(TimeOnly, TimeOnly, bool)>();

                    slotsByDate[slotDateOnly].Add((start, end, slotInfo.IsSetup));
                }
            }
            // ✅ 向後相容：使用舊格式 SlotKeys（預設 isSetup = false，單日模式）
            else if (vm.SlotKeys != null && vm.SlotKeys.Count > 0)
            {
                var slotDateOnly = DateOnly.FromDateTime(vm.ReservationDate!.Value.Date);
                slotsByDate[slotDateOnly] = new List<(TimeOnly, TimeOnly, bool)>();

                foreach (var slotKey in vm.SlotKeys)
                {
                    var parts = slotKey.Split('-');
                    if (parts.Length != 2)
                        throw new HttpException($"時段格式錯誤: {slotKey}");

                    if (!TimeOnly.TryParse(parts[0].Trim(), out var start) ||
                        !TimeOnly.TryParse(parts[1].Trim(), out var end))
                        throw new HttpException($"時段格式錯誤: {slotKey}");

                    slotsByDate[slotDateOnly].Add((start, end, false));  // 預設非場佈
                }
            }
            else
            {
                throw new HttpException("必須選擇至少一個時段");
            }

            // ✅ 跨日模式：檢查中間天是否整天空閒
            if (isMultiDay)
            {
                var allDates = slotsByDate.Keys.OrderBy(d => d).ToList();
                if (allDates.Count >= 2)
                {
                    var firstDate = allDates.First();
                    var lastDate = allDates.Last();

                    // 取得會議室的所有時段定義
                    var roomSlotDefs = db.SysRoomPricePeriod
                        .Where(p => p.RoomId == roomId && p.IsEnabled)
                        .OrderBy(p => p.StartTime)
                        .ToList();

                    if (roomSlotDefs.Any())
                    {
                        // ✅ 首日驗證：必須選到最後一個時段
                        var lastSlotDef = roomSlotDefs.Last();
                        var firstDaySlots = slotsByDate[firstDate];
                        var hasLastSlot = firstDaySlots.Any(s =>
                            s.Start.ToTimeSpan() == lastSlotDef.StartTime &&
                            s.End.ToTimeSpan() == lastSlotDef.EndTime);

                        if (!hasLastSlot)
                            throw new HttpException($"跨日預約的首日 ({firstDate:yyyy/MM/dd}) 必須選擇到最後時段，才能與隔天連續");

                        // ✅ 末日驗證：必須從第一個時段開始選
                        var firstSlotDef = roomSlotDefs.First();
                        var lastDaySlots = slotsByDate[lastDate];
                        var hasFirstSlot = lastDaySlots.Any(s =>
                            s.Start.ToTimeSpan() == firstSlotDef.StartTime &&
                            s.End.ToTimeSpan() == firstSlotDef.EndTime);

                        if (!hasFirstSlot)
                            throw new HttpException($"跨日預約的末日 ({lastDate:yyyy/MM/dd}) 必須從第一時段開始選，才能與前一天連續");
                    }

                    // 檢查中間天
                    for (var date = firstDate.AddDays(1); date < lastDate; date = date.AddDays(1))
                    {
                        var hasAnyOccupied = db.ConferenceRoomSlot
                            .Any(s => s.RoomId == roomId
                                   && s.SlotDate == date
                                   && (s.SlotStatus == SlotStatus.Locked || s.SlotStatus == SlotStatus.Reserved)
                                   && (!excludeConferenceId.HasValue || s.ConferenceId != excludeConferenceId.Value));

                        if (hasAnyOccupied)
                            throw new HttpException($"{date:yyyy/MM/dd} 已有其他預約，無法進行跨日預約");
                    }
                }
            }

            // ✅ 各日期衝突檢查
            foreach (var (date, slots) in slotsByDate)
            {
                var query = db.ConferenceRoomSlot
                    .Where(s => s.RoomId == roomId
                             && s.SlotDate == date
                             && (s.SlotStatus == SlotStatus.Locked || s.SlotStatus == SlotStatus.Reserved));

                if (excludeConferenceId.HasValue)
                {
                    query = query.Where(s => s.ConferenceId != excludeConferenceId.Value);
                }

                var occupiedSlots = query
                    .Select(s => new { s.StartTime, s.EndTime })
                    .ToList();

                foreach (var requested in slots)
                {
                    var hasConflict = occupiedSlots.Any(occupied =>
                        requested.Start < occupied.EndTime && requested.End > occupied.StartTime
                    );

                    if (hasConflict)
                        throw new HttpException($"該會議室在 {date:yyyy/MM/dd} 的時段 {requested.Start:HH\\:mm} ~ {requested.End:HH\\:mm} 已被佔用");
                }
            }

            return (roomId, slotsByDate);
        }

        /// <summary>
        /// 建立 Conference 實體(新增用)
        /// </summary>
        private Conference CreateConferenceEntity(Guid conferenceId, InsertVM vm, Guid userId)
        {

            var room = db.SysRoom
    .AsNoTracking()
    .FirstOrDefault(r => r.Id == vm.RoomId!.Value);

            if (room == null)
                throw new HttpException("會議室不存在");

            Console.WriteLine($"📝 [CreateReservation] 會議室: {room.Name}, 分院ID: {room.DepartmentId}");


            // 取得審核鏈
            var approvalChain = service.RoomApprovalLevelService.GetApprovalChain(vm.RoomId!.Value);
            var totalLevels = approvalChain.Count;

            return new Conference
            {
                Id = conferenceId,
                Name = vm.Name,
                UsageType = vm.UsageType ?? 1,
                MCU = null,
                Recording = false,
                Description = vm.Description,
                ExpectedAttendees = vm.ExpectedAttendees,  // ✅ 預計到達人數
                OrganizerUnit = vm.OrganizerUnit,
                Chairman = vm.Chairman,
                StartTime = null,
                EndTime = null,
                DurationHH = vm.DurationHH ?? 0,
                DurationSS = vm.DurationSS ?? 0,
                RRule = null,
                Status = 1,
                DepartmentId = room.DepartmentId,
                ReservationStatus = ReservationStatus.PendingApproval,
                CurrentApprovalLevel = 0,  // ✅ 尚未開始審核
                TotalApprovalLevels = totalLevels,  // ✅ 總共幾關
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
                ParkingTicketCount = vm.ParkingTicketCount ?? 0,
                ParkingTicketCost = vm.ParkingTicketCost ?? 0,
                TotalAmount = (int)(vm.TotalAmount ?? 0),
                CreateBy = userId,
                CreateAt = DateTime.Now,
                UpdateAt = DateTime.Now,
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
                },
                // ✅ 快照審核鏈到 ApprovalHistory
                ApprovalHistory = approvalChain.Select(x => new ConferenceApprovalHistory
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = conferenceId,
                    Level = x.Level,
                    ApproverId = x.ApproverId,
                    Status = ApprovalStatus.Pending,
                    CreateAt = DateTime.Now
                }).ToList()
            };
        }

        /// <summary>
        /// 更新 Conference 實體(更新用)
        /// </summary>
        private void UpdateConferenceEntity(Conference conference, InsertVM vm)
        {
            // ✅ 查詢會議室取得分院資訊
            var room = db.SysRoom
                .AsNoTracking()
                .FirstOrDefault(r => r.Id == vm.RoomId!.Value);

            if (room == null)
                throw new HttpException("會議室不存在");

            Console.WriteLine($"📝 [UpdateReservation] 會議室: {room.Name}, 分院ID: {room.DepartmentId}");

            conference.Name = vm.Name;
            conference.Description = vm.Description;
            conference.ExpectedAttendees = vm.ExpectedAttendees;  // ✅ 預計到達人數
            conference.OrganizerUnit = vm.OrganizerUnit;
            conference.Chairman = vm.Chairman;
            conference.DepartmentId = room.DepartmentId;  // ✅ 更新分院ID
            conference.PaymentMethod = vm.PaymentMethod;
            conference.DepartmentCode = vm.DepartmentCode;
            conference.RoomCost = (int)(vm.RoomCost ?? 0);
            conference.EquipmentCost = (int)(vm.EquipmentCost ?? 0);
            conference.BoothCost = (int)(vm.BoothCost ?? 0);
            conference.ParkingTicketCount = vm.ParkingTicketCount ?? 0;
            conference.ParkingTicketCost = vm.ParkingTicketCost ?? 0;
            conference.TotalAmount = (int)(vm.TotalAmount ?? 0);
            conference.DurationHH = vm.DurationHH ?? 0;
            conference.DurationSS = vm.DurationSS ?? 0;
            conference.UpdateAt = DateTime.Now;
        }

        /// <summary>
        /// 建立會議室時段記錄（支援多日）
        /// </summary>
        private void CreateRoomSlots(Guid conferenceId, Guid roomId, Dictionary<DateOnly, List<(TimeOnly Start, TimeOnly End, bool IsSetup)>> slotsByDate)
        {
            // ✅ 查詢該會議室的所有時段價格設定
            var roomSlotPrices = db.SysRoomPricePeriod
                .Where(rs => rs.RoomId == roomId && rs.IsEnabled)
                .ToList();

            foreach (var (slotDate, requestedSlots) in slotsByDate)
            {
                // ✅ 查詢該日期是否為假日（包含國定假日、補班日判斷）
                var isHoliday = service.HolidayService.IsHoliday(slotDate);

                foreach (var (start, end, isSetup) in requestedSlots)
                {
                    var startTimeSpan = start.ToTimeSpan();
                    var endTimeSpan = end.ToTimeSpan();

                    // ✅ 找到對應的時段價格
                    var priceInfo = roomSlotPrices.FirstOrDefault(rs =>
                        rs.StartTime == startTimeSpan && rs.EndTime == endTimeSpan
                    );

                    // ✅ 根據 isSetup 決定價格
                    decimal price = 0;
                    if (priceInfo != null)
                    {
                        if (isSetup && priceInfo.SetupPrice.HasValue)
                        {
                            // 場佈價格（固定，不分平假日）
                            price = priceInfo.SetupPrice.Value;
                        }
                        else if (isHoliday && priceInfo.HolidayPrice.HasValue)
                        {
                            // 假日價格
                            price = priceInfo.HolidayPrice.Value;
                        }
                        else
                        {
                            // 平日價格
                            price = priceInfo.Price;
                        }
                    }

                    var slot = new ConferenceRoomSlot
                    {
                        Id = Guid.NewGuid(),
                        ConferenceId = conferenceId,
                        RoomId = roomId,
                        SlotDate = slotDate,
                        StartTime = start,
                        EndTime = end,
                        Price = price,
                        PricingType = PricingType.Period,
                        SlotStatus = SlotStatus.Locked,
                        LockedAt = DateTime.Now,
                        IsSetup = isSetup
                    };

                    db.ConferenceRoomSlot.Add(slot);
                }
            }
        }

        private void SaveAttachments(Guid conferenceId, List<InsertVM.AttachmentVM> attachments, Guid userId)
        {
            foreach (var attachment in attachments)
            {
                // ✅ 驗證檔案
                if (string.IsNullOrWhiteSpace(attachment.Base64Data))
                    continue;

                // ✅ 儲存檔案到伺服器
                var filePath = SaveFileToServer(attachment.FileName, attachment.Base64Data);

                // ✅ 計算檔案大小
                var fileBytes = Convert.FromBase64String(attachment.Base64Data);
                var fileSize = fileBytes.Length;

                // ✅ 取得 MIME Type
                var mimeType = GetMimeType(attachment.FileName);

                // ✅ 建立附件記錄
                var attachmentEntity = new ConferenceAttachment
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = conferenceId,
                    AttachmentType = attachment.Type,
                    FileName = attachment.FileName,
                    FilePath = filePath,
                    FileSize = fileSize,
                    MimeType = mimeType,
                    UploadedAt = DateTime.Now,
                    UploadedBy = userId
                };

                db.ConferenceAttachment.Add(attachmentEntity);
            }
        }

        /// <summary>
        /// 儲存檔案到伺服器
        /// </summary>
        private string SaveFileToServer(string fileName, string base64Data)
        {
            try
            {
                // ✅ 產生唯一檔名
                var fileExtension = Path.GetExtension(fileName);
                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

                // ✅ 建立儲存路徑 (例如: /uploads/conference-attachments/2026/01/)
                var yearMonth = DateTime.Now.ToString("yyyy/MM");
                var uploadDir = Path.Combine("wwwroot", "uploads", "conference-attachments", yearMonth);

                // ✅ 確保目錄存在
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                // ✅ 完整檔案路徑
                var fullPath = Path.Combine(uploadDir, uniqueFileName);

                // ✅ 將 Base64 解碼並儲存
                var fileBytes = Convert.FromBase64String(base64Data);
                File.WriteAllBytes(fullPath, fileBytes);

                // ✅ 回傳相對路徑 (給前端用)
                return $"/uploads/conference-attachments/{yearMonth}/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                throw new HttpException($"檔案儲存失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 取得 MIME Type
        /// </summary>
        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }


        /// <summary>
        /// 建立設備和攤位關聯（支援多日）
        /// </summary>
        private void CreateEquipmentLinks(Guid conferenceId, List<Guid>? equipmentIds, List<Guid>? boothIds, Dictionary<DateOnly, List<(TimeOnly Start, TimeOnly End, bool IsSetup)>> slotsByDate)
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

            // ✅ 對每個日期的每個時段都要建立設備關聯
            foreach (var (slotDate, slots) in slotsByDate)
            {
                foreach (var (start, end, _) in slots)
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
                                SlotDate = slotDate,  // ✅ 日期
                                StartTime = start,    // ✅ 開始時間
                                EndTime = end,        // ✅ 結束時間
                                EquipmentStatus = 1,  // ✅ 鎖定中
                                LockedAt = DateTime.Now,
                                CreatedAt = DateTime.Now
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
                                SlotDate = slotDate,  // ✅ 日期
                                StartTime = start,    // ✅ 開始時間
                                EndTime = end,        // ✅ 結束時間
                                EquipmentStatus = 1,  // ✅ 鎖定中
                                LockedAt = DateTime.Now,
                                CreatedAt = DateTime.Now
                            });
                        }
                    }
                }
            }
        }
    }
}
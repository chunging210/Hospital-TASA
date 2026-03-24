using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TASA.Models.Enums;

namespace TASA.Models
{
    /// <summary>
    /// 預約審核紀錄（快照每關審核狀態）
    /// </summary>
    [Table("ConferenceApprovalHistory")]
    public partial class ConferenceApprovalHistory
    {
        /// <summary>
        /// 流水號
        /// </summary>
        [Column(TypeName = "int(10) unsigned")]
        public uint No { get; set; }

        /// <summary>
        /// 主鍵
        /// </summary>
        [Key]
        [Column(TypeName = "char(36)")]
        public Guid Id { get; set; }

        /// <summary>
        /// 預約ID
        /// </summary>
        [Required]
        [Column(TypeName = "char(36)")]
        public Guid ConferenceId { get; set; }

        /// <summary>
        /// 審核順序 (1, 2, 3...)
        /// </summary>
        [Required]
        [Column(TypeName = "int(11)")]
        public int Level { get; set; }

        /// <summary>
        /// 審核人ID（快照自 SysRoomApprovalLevel）
        /// </summary>
        [Required]
        [Column(TypeName = "char(36)")]
        public Guid ApproverId { get; set; }

        /// <summary>
        /// 審核狀態 (0=待審核, 1=已核准, 2=已拒絕)
        /// </summary>
        [Required]
        [Column(TypeName = "tinyint(3) unsigned")]
        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

        /// <summary>
        /// 審核時間
        /// </summary>
        [Column(TypeName = "datetime")]
        public DateTime? ApprovedAt { get; set; }

        /// <summary>
        /// 實際審核人ID（可能是代理人）
        /// </summary>
        [Column(TypeName = "char(36)")]
        public Guid? ApprovedBy { get; set; }

        /// <summary>
        /// 拒絕原因
        /// </summary>
        [Column(TypeName = "text")]
        public string? Reason { get; set; }

        /// <summary>
        /// 折扣金額（每關可設）
        /// </summary>
        [Column(TypeName = "int(11)")]
        public int? DiscountAmount { get; set; }

        /// <summary>
        /// 折扣原因
        /// </summary>
        [Column(TypeName = "text")]
        public string? DiscountReason { get; set; }

        /// <summary>
        /// 繳費期限（最後一關可設）
        /// </summary>
        [Column(TypeName = "datetime")]
        public DateTime? PaymentDeadline { get; set; }

        /// <summary>
        /// 建立時間（快照時間）
        /// </summary>
        [Required]
        [Column(TypeName = "datetime")]
        public DateTime CreateAt { get; set; }

        // ========= Navigation Properties =========

        /// <summary>
        /// 預約
        /// </summary>
        [ForeignKey(nameof(ConferenceId))]
        public virtual Conference Conference { get; set; } = null!;

        /// <summary>
        /// 指定審核人
        /// </summary>
        [ForeignKey(nameof(ApproverId))]
        public virtual AuthUser Approver { get; set; } = null!;

        /// <summary>
        /// 實際審核人（可能是代理人）
        /// </summary>
        [ForeignKey(nameof(ApprovedBy))]
        public virtual AuthUser? ApprovedByUser { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TASA.Models
{
    /// <summary>
    /// 會議室審核關卡定義
    /// </summary>
    [Table("SysRoomApprovalLevel")]
    public partial class SysRoomApprovalLevel
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
        /// 會議室ID
        /// </summary>
        [Required]
        [Column(TypeName = "char(36)")]
        public Guid RoomId { get; set; }

        /// <summary>
        /// 審核順序 (1, 2, 3...)
        /// </summary>
        [Required]
        [Column(TypeName = "int(11)")]
        public int Level { get; set; }

        /// <summary>
        /// 審核人ID
        /// </summary>
        [Required]
        [Column(TypeName = "char(36)")]
        public Guid ApproverId { get; set; }

        /// <summary>
        /// 建立時間
        /// </summary>
        [Required]
        [Column(TypeName = "datetime")]
        public DateTime CreateAt { get; set; }

        /// <summary>
        /// 建立者
        /// </summary>
        [Required]
        [Column(TypeName = "char(36)")]
        public Guid CreateBy { get; set; }

        /// <summary>
        /// 刪除時間
        /// </summary>
        [Column(TypeName = "datetime")]
        public DateTime? DeleteAt { get; set; }

        // ========= Navigation Properties =========

        /// <summary>
        /// 會議室
        /// </summary>
        [ForeignKey(nameof(RoomId))]
        public virtual SysRoom Room { get; set; } = null!;

        /// <summary>
        /// 審核人
        /// </summary>
        [ForeignKey(nameof(ApproverId))]
        public virtual AuthUser Approver { get; set; } = null!;
    }
}

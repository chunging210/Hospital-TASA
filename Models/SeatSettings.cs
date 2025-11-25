// Models/SeatSetting.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TASA.Models
{
    [Index("Id", Name = "Id", IsUnique = true)]
    public partial class SeatSettings
    {
        /// <summary>
        /// 流水號
        /// </summary>
        [Key]
        [Column(TypeName = "int(10) unsigned")]
        public uint No { get; set; }

        /// <summary>
        /// ID
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Logo 檔案路徑
        /// </summary>
        [StringLength(500)]
        public string? LogoPath { get; set; }

        /// <summary>
        /// 小字體大小
        /// </summary>
        public int FontSizeSmall { get; set; }

        /// <summary>
        /// 中字體大小
        /// </summary>
        public int FontSizeMedium { get; set; }

        /// <summary>
        /// 大字體大小
        /// </summary>
        public int FontSizeLarge { get; set; }

        /// <summary>
        /// 是否啟用
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// 建立時間
        /// </summary>
        [Column(TypeName = "datetime")]
        public DateTime CreateAt { get; set; }

        /// <summary>
        /// 建立者
        /// </summary>
        public Guid CreateBy { get; set; }

        /// <summary>
        /// 更新時間
        /// </summary>
        [Column(TypeName = "datetime")]
        public DateTime? UpdateAt { get; set; }

        /// <summary>
        /// 更新者
        /// </summary>
        public Guid? UpdateBy { get; set; }

        /// <summary>
        /// 刪除時間
        /// </summary>
        [Column(TypeName = "datetime")]
        public DateTime? DeleteAt { get; set; }

        // ========================================
        // 導覽屬性
        // ========================================
        [ForeignKey("CreateBy")]
        [InverseProperty("SeatSetting")]
        public virtual AuthUser? CreateByNavigation { get; set; }
    }
}
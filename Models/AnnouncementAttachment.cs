using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TASA.Models;

/// <summary>
/// 公告附件表
/// </summary>
public partial class AnnouncementAttachment
{
    /// <summary>
    /// 附件ID
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// FK → Announcement
    /// </summary>
    public Guid AnnouncementId { get; set; }

    /// <summary>
    /// 原始檔名（顯示用）
    /// </summary>
    [Required]
    [StringLength(255)]
    public string FileName { get; set; }

    /// <summary>
    /// 實際儲存檔名（避免重複/特殊字元）
    /// </summary>
    [Required]
    [StringLength(255)]
    public string StoredFileName { get; set; }

    /// <summary>
    /// 副檔名：jpg / png / pdf
    /// </summary>
    [Required]
    [StringLength(10)]
    public string FileType { get; set; }

    /// <summary>
    /// 檔案大小（bytes）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreateAt { get; set; }

    /// <summary>
    /// 所屬公告
    /// </summary>
    [ForeignKey("AnnouncementId")]
    [InverseProperty("Attachments")]
    public virtual Announcement Announcement { get; set; }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TASA.Models;

/// <summary>
/// 公告主表
/// </summary>
public partial class Announcement
{
    /// <summary>
    /// 公告ID
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// 標題
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Title { get; set; }

    /// <summary>
    /// 富文字內容
    /// </summary>
    [Required]
    public string Content { get; set; }

    /// <summary>
    /// 是否置頂（全系統只允許一則）
    /// </summary>
    public bool IsPinned { get; set; } = false;

    /// <summary>
    /// 是否預設展開（進頁面時自動展開內容）
    /// </summary>
    public bool IsDefaultExpanded { get; set; } = false;

    /// <summary>
    /// 是否啟用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 到期日，NULL 表示永久有效
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreateAt { get; set; }

    /// <summary>
    /// 建立者
    /// </summary>
    public Guid? CreateBy { get; set; }

    /// <summary>
    /// 更新時間
    /// </summary>
    public DateTime? UpdateAt { get; set; }

    /// <summary>
    /// 刪除時間（軟刪除）
    /// </summary>
    public DateTime? DeleteAt { get; set; }

    /// <summary>
    /// 附件清單
    /// </summary>
    [InverseProperty("Announcement")]
    public virtual ICollection<AnnouncementAttachment> Attachments { get; set; } = new List<AnnouncementAttachment>();
}

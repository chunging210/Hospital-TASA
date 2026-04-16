using System;
using System.ComponentModel.DataAnnotations;

namespace TASA.Models;

/// <summary>
/// 超連結表
/// </summary>
public partial class QuickLink
{
    /// <summary>
    /// 連結ID
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// 顯示文字
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Title { get; set; }

    /// <summary>
    /// 連結網址
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Url { get; set; }

    /// <summary>
    /// 排序（數字越小越前面）
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreateAt { get; set; }

    /// <summary>
    /// 更新時間
    /// </summary>
    public DateTime? UpdateAt { get; set; }

    /// <summary>
    /// 刪除時間（軟刪除）
    /// </summary>
    public DateTime? DeleteAt { get; set; }
}

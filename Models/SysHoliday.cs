using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TASA.Models;

/// <summary>
/// 國定假日/特殊假日表
/// </summary>
[Index("Date", Name = "IX_SysHoliday_Date")]
public partial class SysHoliday
{
    /// <summary>
    /// 記錄ID
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// 日期
    /// </summary>
    [Required]
    public DateOnly Date { get; set; }

    /// <summary>
    /// 假日名稱
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    /// <summary>
    /// 是否為補班日（補班日使用平日價格）
    /// </summary>
    public bool IsWorkday { get; set; } = false;

    /// <summary>
    /// 來源：API=政府API自動匯入, Manual=手動新增
    /// </summary>
    [StringLength(20)]
    public string Source { get; set; } = "Manual";

    /// <summary>
    /// 年度
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// 是否啟用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

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
}

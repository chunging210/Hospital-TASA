#nullable disable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TASA.Models;

/// <summary>
/// 時段制子區間 — 屬於某個主區間（SysRoomPricePeriod）的可選時間切片
/// </summary>
[Index(nameof(PricePeriodId), Name = "idx_price_period_slot")]
public partial class SysRoomPricePeriodSlot
{
    /// <summary>
    /// 子區間ID
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// 所屬主區間ID（FK → SysRoomPricePeriod）
    /// </summary>
    public Guid PricePeriodId { get; set; }

    /// <summary>
    /// 子區間開始時間
    /// </summary>
    [Column(TypeName = "time")]
    public TimeSpan StartTime { get; set; }

    /// <summary>
    /// 子區間結束時間
    /// </summary>
    [Column(TypeName = "time")]
    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime CreateAt { get; set; }

    /* ===============================
     * Navigation Properties
     * =============================== */

    [ForeignKey(nameof(PricePeriodId))]
    [InverseProperty("Slots")]
    public virtual SysRoomPricePeriod PricePeriod { get; set; }
}

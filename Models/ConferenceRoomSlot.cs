#nullable disable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TASA.Models.Enums;

namespace TASA.Models;

[Index(nameof(RoomId), nameof(SlotDate), nameof(StartTime), Name = "idx_room_date")]
public partial class ConferenceRoomSlot
{
    /// <summary>
    /// 占用ID
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// 會議ID（對應 Conference.Id）
    /// </summary>
    public Guid ConferenceId { get; set; }

    /// <summary>
    /// 會議室ID
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// 使用日期
    /// </summary>
    [Column(TypeName = "date")]
    public DateOnly SlotDate { get; set; }

    /// <summary>
    /// 開始時間
    /// </summary>
    [Column(TypeName = "time")]
    public TimeOnly StartTime { get; set; }

    /// <summary>
    /// 結束時間
    /// </summary>
    [Column(TypeName = "time")]
    public TimeOnly EndTime { get; set; }

    /// <summary>
    /// 實際價格
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    /// <summary>
    /// 收費方式(0=Hourly,1=Period)
    /// </summary>
    public PricingType PricingType { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime CreateAt { get; set; }

    /* ===============================
     * Navigation
     * =============================== */

    [ForeignKey(nameof(ConferenceId))]
    public virtual Conference Conference { get; set; }

    [ForeignKey(nameof(RoomId))]
    public virtual SysRoom Room { get; set; }
}

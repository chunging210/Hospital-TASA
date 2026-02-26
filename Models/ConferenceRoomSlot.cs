#nullable disable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TASA.Models.Enums;

namespace TASA.Models;

[Index(nameof(RoomId), nameof(SlotDate), nameof(StartTime), Name = "idx_room_date")]
[Index(nameof(RoomId), nameof(SlotDate), nameof(SlotStatus), Name = "idx_slot_availability")] // ✅ 新增
[Index(nameof(ConferenceId), nameof(SlotStatus), Name = "idx_conference_slots")] // ✅ 新增
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
    public Guid? ConferenceId { get; set; } // ✅ 改為 nullable (未被預約時為 NULL)

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
    /// 時段狀態 (0=可用, 1=審核中, 2=預約成功, 3=已釋放) - ✅ 新增
    /// </summary>
    [Column(TypeName = "tinyint(1) unsigned")]
    public SlotStatus SlotStatus { get; set; } = SlotStatus.Available;

    /// <summary>
    /// 時段被鎖定時間 - ✅ 新增
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime? LockedAt { get; set; }

    /// <summary>
    /// 時段被釋放時間 - ✅ 新增
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime? ReleasedAt { get; set; }

    /// <summary>
    /// 是否為場地佈置
    /// </summary>
    public bool IsSetup { get; set; } = false;

    /// <summary>
    /// 建立時間
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime CreateAt { get; set; }

    /* ===============================
     * Navigation Properties
     * =============================== */

    [ForeignKey(nameof(ConferenceId))]
    [InverseProperty("ConferenceRoomSlots")] // ✅ 修改 InverseProperty
    public virtual Conference Conference { get; set; }

    [ForeignKey(nameof(RoomId))]
    public virtual SysRoom Room { get; set; }
}
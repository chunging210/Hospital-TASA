using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TASA.Models;
using TASA.Models.Enums;

namespace TASA.Models.Configurations;

public class ConferenceRoomSlotConfiguration
    : IEntityTypeConfiguration<ConferenceRoomSlot>
{
    public void Configure(EntityTypeBuilder<ConferenceRoomSlot> entity)
    {
        entity.ToTable("ConferenceRoomSlot");

        /* ===============================
         * Primary Key
         * =============================== */
        entity.HasKey(e => e.Id)
            .HasName("PRIMARY");

        entity.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasComment("占用ID");

        /* ===============================
         * Columns
         * =============================== */
        entity.Property(e => e.ConferenceId)
            .HasComment("會議ID（對應 Conference.Id，未被預約時為 NULL）"); // ✅ 改為 nullable

        entity.Property(e => e.RoomId)
            .IsRequired()
            .HasComment("會議室ID");

        entity.Property(e => e.SlotDate)
            .HasColumnType("date")
            .HasComment("使用日期");

        entity.Property(e => e.StartTime)
            .HasColumnType("time")
            .HasComment("開始時間");

        entity.Property(e => e.EndTime)
            .HasColumnType("time")
            .HasComment("結束時間");

        entity.Property(e => e.Price)
            .HasColumnType("decimal(10,2)")
            .HasDefaultValue(0)
            .HasComment("實際價格");

        entity.Property(e => e.PricingType)
            .HasDefaultValue(PricingType.Hourly)
            .HasComment("收費方式(0=Hourly,1=Period)");

        // ✅ 新增：時段狀態欄位
        entity.Property(e => e.SlotStatus)
    .HasColumnType("tinyint(1) unsigned")
    .HasDefaultValue(SlotStatus.Available)  // ✅ 直接用 enum
    .HasComment("時段狀態 (0=可用, 1=鎖定中, 2=已預約)");

        entity.Property(e => e.LockedAt)
            .HasColumnType("datetime")
            .HasComment("時段被鎖定時間");

        entity.Property(e => e.ReleasedAt)
            .HasColumnType("datetime")
            .HasComment("時段被釋放時間");

        entity.Property(e => e.IsSetup)
            .HasDefaultValue(false)
            .HasComment("是否為場地布置");

        entity.Property(e => e.CreateAt)
            .HasColumnType("datetime")
            .HasComment("建立時間");

        /* ===============================
         * Indexes
         * =============================== */
        entity.HasIndex(
            e => new { e.RoomId, e.SlotDate, e.StartTime },
            "idx_room_date"
        );

        // ✅ 新增索引：查詢某房間某日期的可用時段
        entity.HasIndex(
            e => new { e.RoomId, e.SlotDate, e.SlotStatus },
            "idx_slot_availability"
        );

        // ✅ 新增索引：查詢某會議的所有時段
        entity.HasIndex(
            e => new { e.ConferenceId, e.SlotStatus },
            "idx_conference_slots"
        );

        /* ===============================
         * Relationships
         * =============================== */

        // ConferenceRoomSlot → Conference
        // FK: ConferenceRoomSlot.ConferenceId (Guid?)
        // PK: Conference.Id (Guid)
        entity.HasOne(e => e.Conference)
            .WithMany(c => c.ConferenceRoomSlots)
            .HasForeignKey(e => e.ConferenceId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_slot_conf");

        // ConferenceRoomSlot → SysRoom
        entity.HasOne(e => e.Room)
            .WithMany()
            .HasForeignKey(e => e.RoomId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_slot_room");
    }
}
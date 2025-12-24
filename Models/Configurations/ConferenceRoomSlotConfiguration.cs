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
            .IsRequired()
            .HasComment("會議ID（對應 Conference.Id）");

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

        entity.Property(e => e.CreateAt)
            .HasColumnType("datetime")
            .HasComment("建立時間");

        /* ===============================
         * Index
         * =============================== */
        entity.HasIndex(
            e => new { e.RoomId, e.SlotDate, e.StartTime },
            "idx_room_date"
        );

        /* ===============================
         * Relationships
         * =============================== */

        // ConferenceRoomSlot → Conference
        // FK: ConferenceRoomSlot.ConferenceId (Guid)
        // PK: Conference.Id (Guid) ⚠️ 一定要指定
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

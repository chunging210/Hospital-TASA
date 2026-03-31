using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TASA.Models;

namespace TASA.Models.Configurations
{
    public class SysRoomPricePeriodSlotConfiguration : IEntityTypeConfiguration<SysRoomPricePeriodSlot>
    {
        public void Configure(EntityTypeBuilder<SysRoomPricePeriodSlot> entity)
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.PricePeriodId, "idx_price_period_slot");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasComment("子區間ID");

            entity.Property(e => e.PricePeriodId).HasComment("所屬主區間ID");
            entity.Property(e => e.StartTime).HasComment("子區間開始時間");
            entity.Property(e => e.EndTime).HasComment("子區間結束時間");
            entity.Property(e => e.CreateAt).HasComment("建立時間");

            entity.HasOne(d => d.PricePeriod)
                .WithMany(p => p.Slots)
                .HasForeignKey(d => d.PricePeriodId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SysRoomPricePeriodSlot_PricePeriod");
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TASA.Models;

namespace TASA.Models.Configurations;

public class SysNameplateConfiguration : IEntityTypeConfiguration<SysNameplate>
{
    public void Configure(EntityTypeBuilder<SysNameplate> entity)
    {
        entity.HasKey(e => e.Id).HasName("PRIMARY");
        entity.ToTable("SysNameplate");

        entity.Property(e => e.Id).IsRequired().HasColumnType("char(36)");
        entity.Property(e => e.DeviceType).HasColumnType("tinyint(1) unsigned").HasComment("0=分配器, 1=桌牌");
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
entity.Property(e => e.Host).HasMaxLength(50);
        entity.Property(e => e.Port).HasColumnType("int(11)");
        entity.Property(e => e.Mac).HasMaxLength(50);
        entity.Property(e => e.IsEnabled).HasDefaultValue(true);
        entity.Property(e => e.CreateAt).HasColumnType("datetime");
        entity.Property(e => e.DeleteAt).HasColumnType("datetime");

        entity.HasOne(e => e.Distributor)
              .WithMany()
              .HasForeignKey(e => e.DistributorId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}

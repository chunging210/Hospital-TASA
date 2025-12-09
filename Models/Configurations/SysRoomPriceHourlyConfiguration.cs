using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using TASA.Models;

namespace TASA.Models.Configurations
{
    public partial class SysRoomPriceHourlyConfiguration : IEntityTypeConfiguration<SysRoomPriceHourly>
    {
        public void Configure(EntityTypeBuilder<SysRoomPriceHourly> entity)
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");  // ✅ 改成用 Id 當主鍵

            entity.HasIndex(e => e.RoomId, "RoomId");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()  // ✅ Id 自動產生
                .HasComment("記錄ID");

            entity.Property(e => e.RoomId).HasComment("會議室ID");
            entity.Property(e => e.StartTime).HasComment("開始時間");
            entity.Property(e => e.EndTime).HasComment("結束時間");
            entity.Property(e => e.Price)
                .HasDefaultValue(0)
                .HasComment("價格");
            entity.Property(e => e.IsEnabled)
                .HasDefaultValue(true)
                .HasComment("是否開放");
            entity.Property(e => e.CreateAt).HasComment("建立時間");
            entity.Property(e => e.CreateBy).HasComment("建立者");
            entity.Property(e => e.DeleteAt).HasComment("刪除時間");

            entity.HasOne(d => d.Room)
                .WithMany(p => p.SysRoomPriceHourly)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SysRoomPriceHourly_SysRoom");

            OnConfigurePartial(entity);
        }

        partial void OnConfigurePartial(EntityTypeBuilder<SysRoomPriceHourly> entity);
    }
}
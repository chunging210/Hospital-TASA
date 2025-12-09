using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using TASA.Models;


namespace TASA.Models.Configurations
{
    public class SysRoomImageConfiguration : IEntityTypeConfiguration<SysRoomImage>
    {
        public void Configure(EntityTypeBuilder<SysRoomImage> builder)
        {
            builder.ToTable("SysRoomImage");
            builder.HasKey(e => e.Id).HasName("PK_SysRoomImage");

            builder.Property(e => e.Id).HasColumnType("char(36)").IsRequired();
            builder.Property(e => e.RoomId).HasColumnType("char(36)").IsRequired();
            builder.Property(e => e.ImagePath).HasColumnType("varchar(255)").IsRequired();
            builder.Property(e => e.ImageName).HasColumnType("varchar(100)").IsRequired();
            builder.Property(e => e.FileType).HasColumnType("varchar(50)").HasDefaultValue("image");
            builder.Property(e => e.FileSize).HasColumnType("bigint(20)").IsRequired();
            builder.Property(e => e.SortOrder).HasColumnType("int(11)").HasDefaultValue(0);
            builder.Property(e => e.CreateAt).HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.CreateBy).HasColumnType("char(36)").IsRequired();

            builder.HasOne(e => e.Room)
                .WithMany(r => r.Images)
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SysRoomImage_SysRoom");

            builder.HasIndex(e => e.RoomId).HasDatabaseName("IX_SysRoomImage_RoomId");
            builder.HasIndex(e => new { e.RoomId, e.SortOrder }).HasDatabaseName("IX_SysRoomImage_RoomId_SortOrder");
        }
    }
}
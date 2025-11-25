// Models/Configurations/SeatSettingConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TASA.Models;

namespace TASA.Models.Configurations
{
    public partial class SeatSettingsConfiguration : IEntityTypeConfiguration<SeatSettings>
    {
        public void Configure(EntityTypeBuilder<SeatSettings> entity)
        {
            // ========================================
            // 設定主鍵
            // ========================================
            entity.HasKey(e => e.No).HasName("PRIMARY");

            // ========================================
            // 設定索引
            // ========================================
            entity.HasIndex(e => e.Id, "Id").IsUnique();

            // ========================================
            // 設定欄位屬性和註解
            // ========================================
            entity.Property(e => e.No)
                .HasComment("流水號");

            entity.Property(e => e.Id)
                .HasComment("ID");

            entity.Property(e => e.LogoPath)
                .HasMaxLength(500)
                .HasComment("Logo 檔案路徑");

            entity.Property(e => e.FontSizeSmall)
                .HasComment("小字體大小");

            entity.Property(e => e.FontSizeMedium)
                .HasComment("中字體大小");

            entity.Property(e => e.FontSizeLarge)
                .HasComment("大字體大小");

            entity.Property(e => e.IsEnabled)
                .HasComment("是否啟用");

            entity.Property(e => e.CreateAt)
                .HasColumnType("datetime")
                .HasComment("建立時間");

            entity.Property(e => e.CreateBy)
                .HasComment("建立者");

            entity.Property(e => e.UpdateAt)
                .HasColumnType("datetime")
                .HasComment("更新時間");

            entity.Property(e => e.UpdateBy)
                .HasComment("更新者");

            entity.Property(e => e.DeleteAt)
                .HasColumnType("datetime")
                .HasComment("刪除時間");

            // ========================================
            // 設定外鍵關聯 (CreateBy -> AuthUser)
            // ========================================
            entity.HasOne(d => d.CreateByNavigation)
                .WithMany(p => p.SeatSetting)
                .HasPrincipalKey(p => p.Id)
                .HasForeignKey(d => d.CreateBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("SeatSetting_ibfk_1");

            OnConfigurePartial(entity);
        }

        partial void OnConfigurePartial(EntityTypeBuilder<SeatSettings> entity);
    }
}
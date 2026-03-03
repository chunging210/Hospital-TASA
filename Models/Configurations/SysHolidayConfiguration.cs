using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TASA.Models.Configurations
{
    public partial class SysHolidayConfiguration : IEntityTypeConfiguration<SysHoliday>
    {
        public void Configure(EntityTypeBuilder<SysHoliday> entity)
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.Date, "IX_SysHoliday_Date");
            entity.HasIndex(e => e.Year, "IX_SysHoliday_Year");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasComment("記錄ID");

            entity.Property(e => e.Date)
                .HasComment("日期");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("假日名稱");

            entity.Property(e => e.IsWorkday)
                .HasDefaultValue(false)
                .HasComment("是否為補班日");

            entity.Property(e => e.Source)
                .HasMaxLength(20)
                .HasDefaultValue("Manual")
                .HasComment("來源：API/Manual");

            entity.Property(e => e.Year)
                .HasComment("年度");

            entity.Property(e => e.IsEnabled)
                .HasDefaultValue(true)
                .HasComment("是否啟用");

            entity.Property(e => e.CreateAt)
                .HasComment("建立時間");

            entity.Property(e => e.CreateBy)
                .HasComment("建立者");

            entity.Property(e => e.UpdateAt)
                .HasComment("更新時間");

            entity.Property(e => e.DeleteAt)
                .HasComment("刪除時間");

            OnConfigurePartial(entity);
        }

        partial void OnConfigurePartial(EntityTypeBuilder<SysHoliday> entity);
    }
}

// Models/Configurations/CostCenterConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TASA.Models;

namespace TASA.Models.Configurations
{
    public partial class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
    {
        public void Configure(EntityTypeBuilder<CostCenter> entity)
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("CostCenter");

            entity.HasIndex(e => e.Code, "UK_CostCenter_Code")
                .IsUnique();

            entity.HasIndex(e => e.Code, "IX_CostCenter_Code");

            entity.HasIndex(e => e.IsActive, "IX_CostCenter_IsActive");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .HasComment("成本中心ID");

            entity.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(10)
                .HasComment("成本中心代碼");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("成本中心名稱");

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true)
                .HasComment("是否啟用");

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasComment("建立時間");

            entity.Property(e => e.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate()
                .HasComment("更新時間");

            OnConfigurePartial(entity);
        }

        partial void OnConfigurePartial(EntityTypeBuilder<CostCenter> entity);
    }
}
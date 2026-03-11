// Models/Configurations/CostCenterManagerConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TASA.Models;

namespace TASA.Models.Configurations
{
    public partial class CostCenterManagerConfiguration : IEntityTypeConfiguration<CostCenterManager>
    {
        public void Configure(EntityTypeBuilder<CostCenterManager> entity)
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("CostCenterManager");

            entity.HasIndex(e => e.DepartmentId, "IX_CostCenterManager_DepartmentId");
            entity.HasIndex(e => e.ManagerId, "IX_CostCenterManager_ManagerId");
            entity.HasIndex(e => e.CostCenterCode, "IX_CostCenterManager_CostCenterCode");

            entity.Property(e => e.Id)
                .HasComment("成本中心主管ID");

            entity.Property(e => e.CostCenterCode)
                .IsRequired()
                .HasMaxLength(10)
                .HasComment("成本代碼");

            entity.Property(e => e.DepartmentId)
                .IsRequired()
                .HasComment("分院ID");

            entity.Property(e => e.ManagerId)
                .IsRequired()
                .HasComment("主管ID");

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

        partial void OnConfigurePartial(EntityTypeBuilder<CostCenterManager> entity);
    }
}

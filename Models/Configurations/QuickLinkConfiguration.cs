using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TASA.Models.Configurations
{
    public partial class QuickLinkConfiguration : IEntityTypeConfiguration<QuickLink>
    {
        public void Configure(EntityTypeBuilder<QuickLink> entity)
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasComment("連結ID");

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("顯示文字");

            entity.Property(e => e.Url)
                .IsRequired()
                .HasMaxLength(500)
                .HasComment("連結網址");

            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0)
                .HasComment("排序");

            entity.Property(e => e.CreateAt)
                .HasComment("建立時間");

            entity.Property(e => e.UpdateAt)
                .HasComment("更新時間");

            entity.Property(e => e.DeleteAt)
                .HasComment("刪除時間（軟刪除）");

            OnConfigurePartial(entity);
        }

        partial void OnConfigurePartial(EntityTypeBuilder<QuickLink> entity);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TASA.Models.Configurations
{
    public partial class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
    {
        public void Configure(EntityTypeBuilder<Announcement> entity)
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasComment("公告ID");

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200)
                .HasComment("標題");

            entity.Property(e => e.Content)
                .IsRequired()
                .HasColumnType("longtext")
                .HasComment("富文字內容");

            entity.Property(e => e.IsPinned)
                .HasDefaultValue(false)
                .HasComment("是否置頂");

            entity.Property(e => e.IsDefaultExpanded)
                .HasDefaultValue(false)
                .HasComment("是否預設展開");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasComment("是否啟用");

            entity.Property(e => e.EndDate)
                .HasComment("到期日，NULL=永久有效");

            entity.Property(e => e.CreateAt)
                .HasComment("建立時間");

            entity.Property(e => e.CreateBy)
                .HasComment("建立者");

            entity.Property(e => e.UpdateAt)
                .HasComment("更新時間");

            entity.Property(e => e.DeleteAt)
                .HasComment("刪除時間（軟刪除）");

            entity.HasMany(e => e.Attachments)
                .WithOne(a => a.Announcement)
                .HasForeignKey(a => a.AnnouncementId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AnnouncementAttachment_Announcement");

            OnConfigurePartial(entity);
        }

        partial void OnConfigurePartial(EntityTypeBuilder<Announcement> entity);
    }
}

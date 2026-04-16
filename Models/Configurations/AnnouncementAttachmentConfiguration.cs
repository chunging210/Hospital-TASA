using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TASA.Models.Configurations
{
    public partial class AnnouncementAttachmentConfiguration : IEntityTypeConfiguration<AnnouncementAttachment>
    {
        public void Configure(EntityTypeBuilder<AnnouncementAttachment> entity)
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.AnnouncementId, "IX_AnnouncementAttachment_AnnouncementId");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasComment("附件ID");

            entity.Property(e => e.AnnouncementId)
                .HasComment("FK → Announcement");

            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("原始檔名");

            entity.Property(e => e.StoredFileName)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("實際儲存檔名");

            entity.Property(e => e.FileType)
                .IsRequired()
                .HasMaxLength(10)
                .HasComment("副檔名 jpg/png/pdf");

            entity.Property(e => e.FileSize)
                .HasComment("檔案大小(bytes)");

            entity.Property(e => e.CreateAt)
                .HasComment("建立時間");

            OnConfigurePartial(entity);
        }

        partial void OnConfigurePartial(EntityTypeBuilder<AnnouncementAttachment> entity);
    }
}

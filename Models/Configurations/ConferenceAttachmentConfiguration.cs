// Models/Configurations/ConferenceAttachmentConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TASA.Models;
using TASA.Models.Enums;

namespace TASA.Models.Configurations;

public class ConferenceAttachmentConfiguration
    : IEntityTypeConfiguration<ConferenceAttachment>
{
    public void Configure(EntityTypeBuilder<ConferenceAttachment> entity)
    {
        entity.ToTable("ConferenceAttachment");

        /* ===============================
         * Primary Key
         * =============================== */
        entity.HasKey(e => e.Id)
            .HasName("PRIMARY");

        entity.Property(e => e.Id)
            .HasColumnType("char(36)")
            .ValueGeneratedOnAdd()
            .HasComment("附件ID");

        /* ===============================
         * Columns
         * =============================== */
        entity.Property(e => e.ConferenceId)
            .HasColumnType("char(36)")
            .IsRequired()
            .HasComment("會議ID");

        entity.Property(e => e.AttachmentType)
            .HasColumnType("tinyint(1) unsigned")
            .IsRequired()
            .HasComment("附件類型 (1=議程表, 2=會議文件, 3=付款憑證)");

        entity.Property(e => e.FileName)
            .HasMaxLength(255)
            .IsRequired()
            .HasComment("檔案名稱");

        entity.Property(e => e.FilePath)
            .HasMaxLength(500)
            .IsRequired()
            .HasComment("檔案路徑");

        entity.Property(e => e.FileSize)
            .HasColumnType("bigint")
            .HasComment("檔案大小(bytes)");

        entity.Property(e => e.MimeType)
            .HasMaxLength(100)
            .HasComment("檔案MIME類型");

        entity.Property(e => e.UploadedAt)
            .HasColumnType("datetime")
            .IsRequired()
            .HasComment("上傳時間");

        entity.Property(e => e.UploadedBy)
            .HasColumnType("char(36)")
            .IsRequired()
            .HasComment("上傳者ID");

        entity.Property(e => e.DeleteAt)
            .HasColumnType("datetime")
            .HasComment("刪除時間");

        /* ===============================
         * Indexes
         * =============================== */
        
        // ✅ 索引：查詢某會議的所有附件
        entity.HasIndex(
            e => new { e.ConferenceId, e.AttachmentType },
            "idx_conference_attachments"
        );

        // ✅ 索引：查詢未刪除的附件
        entity.HasIndex(
            e => new { e.ConferenceId, e.DeleteAt },
            "idx_conference_active_attachments"
        );

        // ✅ 索引：查詢某使用者上傳的附件
        entity.HasIndex(
            e => e.UploadedBy,
            "idx_uploaded_by"
        );

        /* ===============================
         * Relationships
         * =============================== */

        // ConferenceAttachment → Conference
        // FK: ConferenceAttachment.ConferenceId (Guid)
        // PK: Conference.Id (Guid)
        entity.HasOne(e => e.Conference)
            .WithMany(c => c.Attachments)
            .HasForeignKey(e => e.ConferenceId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ConferenceAttachment_Conference");

        // ConferenceAttachment → AuthUser
        // FK: ConferenceAttachment.UploadedBy (Guid)
        // PK: AuthUser.Id (Guid)
        entity.HasOne(e => e.UploadedByNavigation)
            .WithMany()
            .HasForeignKey(e => e.UploadedBy)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ConferenceAttachment_User");
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TASA.Models;
using TASA.Models.Enums;

namespace TASA.Models.Configurations;

public partial class ConferencePaymentOrderConfiguration : IEntityTypeConfiguration<ConferencePaymentOrder>
{
    public void Configure(EntityTypeBuilder<ConferencePaymentOrder> entity)
    {
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("ConferencePaymentOrder");

        entity.HasIndex(e => e.Status, "idx_order_status");
        entity.HasIndex(e => e.UploadedBy, "idx_order_uploaded_by");
        entity.HasIndex(e => e.ReviewedBy, "idx_order_reviewed_by");

        entity.Property(e => e.Id)
            .IsRequired()
            .HasColumnType("char(36)")
            .HasComment("訂單ID");

        entity.Property(e => e.PaymentMethod)
            .HasMaxLength(50)
            .HasComment("付款方式 (臨櫃/匯款)");

        entity.Property(e => e.PaymentType)
            .HasMaxLength(50)
            .HasComment("付款類型標籤");

        entity.Property(e => e.LastFiveDigits)
            .HasMaxLength(5)
            .HasComment("轉帳末五碼");

        entity.Property(e => e.TransferAmount)
            .HasColumnType("int(11)")
            .HasComment("轉帳金額");

        entity.Property(e => e.TransferAt)
            .HasColumnType("datetime")
            .HasComment("轉帳時間");

        entity.Property(e => e.FilePath)
            .HasMaxLength(500)
            .HasComment("付款憑證檔案路徑");

        entity.Property(e => e.FileName)
            .HasMaxLength(255)
            .HasComment("付款憑證檔案名稱");

        entity.Property(e => e.DiscountProofPath)
            .HasMaxLength(500)
            .HasComment("優惠證明檔案路徑");

        entity.Property(e => e.DiscountProofName)
            .HasMaxLength(255)
            .HasComment("優惠證明檔案名稱");

        entity.Property(e => e.Status)
            .HasColumnType("tinyint(1) unsigned")
            .HasDefaultValue(PaymentOrderStatus.PendingVerification)
            .HasComment("訂單狀態 (1=待查帳, 2=已收款, 3=已退回)");

        entity.Property(e => e.Note)
            .HasColumnType("text")
            .HasComment("備註");

        entity.Property(e => e.RejectReason)
            .HasColumnType("text")
            .HasComment("退回原因");

        entity.Property(e => e.UploadedAt)
            .HasColumnType("datetime")
            .HasComment("上傳時間");

        entity.Property(e => e.UploadedBy)
            .IsRequired()
            .HasColumnType("char(36)")
            .HasComment("上傳者");

        entity.Property(e => e.ReviewedAt)
            .HasColumnType("datetime")
            .HasComment("審核時間");

        entity.Property(e => e.ReviewedBy)
            .HasColumnType("char(36)")
            .HasComment("審核者");

        entity.Property(e => e.CreateAt)
            .HasColumnType("datetime")
            .HasComment("建立時間");

        entity.Property(e => e.CreateBy)
            .IsRequired()
            .HasColumnType("char(36)")
            .HasComment("建立者");

        entity.Property(e => e.DeleteAt)
            .HasColumnType("datetime")
            .HasComment("刪除時間");

        entity.HasOne(d => d.UploadedByNavigation)
            .WithMany(p => p.ConferencePaymentOrderUploadedBy)
            .HasPrincipalKey(p => p.Id)
            .HasForeignKey(d => d.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("ConferencePaymentOrder_ibfk_1");

        entity.HasOne(d => d.ReviewedByNavigation)
            .WithMany(p => p.ConferencePaymentOrderReviewedBy)
            .HasPrincipalKey(p => p.Id)
            .HasForeignKey(d => d.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("ConferencePaymentOrder_ibfk_2");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<ConferencePaymentOrder> entity);
}

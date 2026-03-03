using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TASA.Models.Enums;

namespace TASA.Models.Configurations
{
    public partial class ConferenceApprovalHistoryConfiguration : IEntityTypeConfiguration<ConferenceApprovalHistory>
    {
        public void Configure(EntityTypeBuilder<ConferenceApprovalHistory> entity)
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.No, "No");
            entity.HasIndex(e => new { e.ConferenceId, e.Level }, "IX_ConferenceId_Level");
            entity.HasIndex(e => new { e.ApproverId, e.Status }, "IX_ApproverId_Status");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasComment("主鍵");

            entity.Property(e => e.No)
                .ValueGeneratedOnAdd()
                .HasComment("流水號");

            entity.Property(e => e.ConferenceId)
                .HasComment("預約ID");

            entity.Property(e => e.Level)
                .HasComment("審核順序");

            entity.Property(e => e.ApproverId)
                .HasComment("審核人ID");

            entity.Property(e => e.Status)
                .HasColumnType("tinyint(1) unsigned")
                .HasDefaultValue(ApprovalStatus.Pending)
                .HasComment("審核狀態");

            entity.Property(e => e.ApprovedAt)
                .HasComment("審核時間");

            entity.Property(e => e.ApprovedBy)
                .HasComment("實際審核人ID");

            entity.Property(e => e.Reason)
                .HasComment("拒絕原因");

            entity.Property(e => e.DiscountAmount)
                .HasComment("折扣金額");

            entity.Property(e => e.DiscountReason)
                .HasComment("折扣原因");

            entity.Property(e => e.PaymentDeadline)
                .HasComment("繳費期限");

            entity.Property(e => e.CreateAt)
                .HasComment("建立時間");

            // 關聯：預約
            entity.HasOne(e => e.Conference)
                .WithMany(c => c.ApprovalHistory)
                .HasForeignKey(e => e.ConferenceId)
                .HasPrincipalKey(c => c.Id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ConferenceApprovalHistory_Conference");

            // 關聯：指定審核人
            entity.HasOne(e => e.Approver)
                .WithMany()
                .HasForeignKey(e => e.ApproverId)
                .HasPrincipalKey(u => u.Id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ConferenceApprovalHistory_Approver");

            // 關聯：實際審核人
            entity.HasOne(e => e.ApprovedByUser)
                .WithMany()
                .HasForeignKey(e => e.ApprovedBy)
                .HasPrincipalKey(u => u.Id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ConferenceApprovalHistory_ApprovedBy");
        }
    }
}

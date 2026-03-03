using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TASA.Models.Configurations
{
    public partial class SysRoomApprovalLevelConfiguration : IEntityTypeConfiguration<SysRoomApprovalLevel>
    {
        public void Configure(EntityTypeBuilder<SysRoomApprovalLevel> entity)
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.No, "No");
            entity.HasIndex(e => new { e.RoomId, e.Level }, "IX_RoomId_Level");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasComment("主鍵");

            entity.Property(e => e.No)
                .ValueGeneratedOnAdd()
                .HasComment("流水號");

            entity.Property(e => e.RoomId)
                .HasComment("會議室ID");

            entity.Property(e => e.Level)
                .HasComment("審核順序");

            entity.Property(e => e.ApproverId)
                .HasComment("審核人ID");

            entity.Property(e => e.CreateAt)
                .HasComment("建立時間");

            entity.Property(e => e.CreateBy)
                .HasComment("建立者");

            entity.Property(e => e.DeleteAt)
                .HasComment("刪除時間");

            // 關聯：會議室
            entity.HasOne(e => e.Room)
                .WithMany(r => r.ApprovalLevels)
                .HasForeignKey(e => e.RoomId)
                .HasPrincipalKey(r => r.Id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SysRoomApprovalLevel_Room");

            // 關聯：審核人
            entity.HasOne(e => e.Approver)
                .WithMany()
                .HasForeignKey(e => e.ApproverId)
                .HasPrincipalKey(u => u.Id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_SysRoomApprovalLevel_Approver");
        }
    }
}

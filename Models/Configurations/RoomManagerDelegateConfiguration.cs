using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TASA.Models.Configurations;

public class RoomManagerDelegateConfiguration
    : IEntityTypeConfiguration<RoomManagerDelegate>
{
    public void Configure(EntityTypeBuilder<RoomManagerDelegate> entity)
    {
        entity.ToTable("RoomManagerDelegate");

        /* ===============================
         * Primary Key
         * =============================== */
        entity.HasKey(e => e.Id)
            .HasName("PRIMARY");

        entity.Property(e => e.Id)
            .HasColumnType("char(36)")
            .ValueGeneratedOnAdd()
            .HasComment("委派ID");

        /* ===============================
         * Columns
         * =============================== */
        entity.Property(e => e.No)
            .ValueGeneratedOnAdd()
            .HasComment("流水號");

        entity.HasIndex(e => e.No, "IX_RoomManagerDelegate_No");

        entity.Property(e => e.RoomId)
            .HasColumnType("char(36)")
            .HasComment("會議室ID (B方案不使用)");

        entity.Property(e => e.ManagerId)
            .HasColumnType("char(36)")
            .IsRequired()
            .HasComment("委派者(管理者)ID");

        entity.Property(e => e.DelegateUserId)
            .HasColumnType("char(36)")
            .IsRequired()
            .HasComment("被委派者(代理人)ID");

        entity.Property(e => e.StartDate)
            .IsRequired()
            .HasComment("委派開始日期");

        entity.Property(e => e.EndDate)
            .IsRequired()
            .HasComment("委派結束日期");

        entity.Property(e => e.IsEnabled)
            .HasDefaultValue(true)
            .HasComment("是否啟用");

        entity.Property(e => e.CreateAt)
            .HasColumnType("datetime")
            .IsRequired()
            .HasComment("建立時間");

        entity.Property(e => e.CreateBy)
            .HasColumnType("char(36)")
            .IsRequired()
            .HasComment("建立者");

        entity.Property(e => e.DeleteAt)
            .HasColumnType("datetime")
            .HasComment("刪除時間");

        /* ===============================
         * Indexes
         * =============================== */
        entity.HasIndex(
            e => new { e.ManagerId, e.IsEnabled, e.DeleteAt, e.StartDate, e.EndDate },
            "idx_manager_delegate_active"
        );

        entity.HasIndex(
            e => new { e.DelegateUserId, e.IsEnabled, e.DeleteAt },
            "idx_delegate_user"
        );

        /* ===============================
         * Relationships
         * =============================== */
        entity.HasOne(e => e.Manager)
            .WithMany()
            .HasForeignKey(e => e.ManagerId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Delegate_Manager");

        entity.HasOne(e => e.DelegateUser)
            .WithMany()
            .HasForeignKey(e => e.DelegateUserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Delegate_User");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TASA.Models;

namespace TASA.Models.Configurations;

public partial class ConferencePaymentOrderItemConfiguration : IEntityTypeConfiguration<ConferencePaymentOrderItem>
{
    public void Configure(EntityTypeBuilder<ConferencePaymentOrderItem> entity)
    {
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("ConferencePaymentOrderItem");

        entity.HasIndex(e => e.OrderId, "idx_order_item_order_id");
        entity.HasIndex(e => e.ConferenceId, "idx_order_item_conference_id");

        entity.Property(e => e.Id)
            .IsRequired()
            .HasColumnType("char(36)")
            .HasComment("明細ID");

        entity.Property(e => e.OrderId)
            .IsRequired()
            .HasColumnType("char(36)")
            .HasComment("訂單ID");

        entity.Property(e => e.ConferenceId)
            .IsRequired()
            .HasColumnType("char(36)")
            .HasComment("會議ID");

        entity.Property(e => e.CreateAt)
            .HasColumnType("datetime")
            .HasComment("建立時間");

        entity.HasOne(d => d.Order)
            .WithMany(p => p.Items)
            .HasPrincipalKey(p => p.Id)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("ConferencePaymentOrderItem_ibfk_1");

        entity.HasOne(d => d.Conference)
            .WithMany(p => p.ConferencePaymentOrderItems)
            .HasPrincipalKey(p => p.Id)
            .HasForeignKey(d => d.ConferenceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("ConferencePaymentOrderItem_ibfk_2");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<ConferencePaymentOrderItem> entity);
}

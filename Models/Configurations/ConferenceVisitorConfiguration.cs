using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using TASA.Models;

namespace TASA.Models.Configurations
{
    public partial class ConferenceVisitorConfiguration : IEntityTypeConfiguration<ConferenceVisitor>
    {
        public void Configure(EntityTypeBuilder<ConferenceVisitor> entity)
        {
            entity.HasKey(e => new { e.ConferenceId, e.VisitorId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.HasIndex(e => e.VisitorId, "FK_ConferenceVisitor_Visitor");

            entity.Property(e => e.ConferenceId).HasComment("會議ID");
            entity.Property(e => e.VisitorId).HasComment("訪客ID");

            entity.HasOne(d => d.Conference).WithMany(p => p.ConferenceVisitors)
                .HasPrincipalKey(p => p.Id)
                .HasForeignKey(d => d.ConferenceId)
                .HasConstraintName("FK_ConferenceVisitor_Conference")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Visitor).WithMany(p => p.ConferenceVisitors)
                .HasPrincipalKey(p => p.Id)
                .HasForeignKey(d => d.VisitorId)
                .HasConstraintName("FK_ConferenceVisitor_Visitor")
                .OnDelete(DeleteBehavior.Cascade);

            OnConfigurePartial(entity);
        }

        partial void OnConfigurePartial(EntityTypeBuilder<ConferenceVisitor> entity);
    }
}
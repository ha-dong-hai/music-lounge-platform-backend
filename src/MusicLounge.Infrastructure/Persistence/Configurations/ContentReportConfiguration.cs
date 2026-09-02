using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> b)
    {
        b.ToTable("content_reports");
        b.HasKey(r => r.Id);

        b.Property(r => r.TargetType).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        b.Property(r => r.ResolutionNote).HasMaxLength(1000);

        b.HasIndex(r => new { r.TargetType, r.TargetId, r.Status });

        b.HasOne(r => r.Reporter)
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(r => r.ResolvedByAdmin)
            .WithMany()
            .HasForeignKey(r => r.ResolvedByAdminId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

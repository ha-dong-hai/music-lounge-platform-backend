using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LoungeShowConfiguration : IEntityTypeConfiguration<LoungeShow>
{
    public void Configure(EntityTypeBuilder<LoungeShow> b)
    {
        b.ToTable("lounge_shows");
        b.HasKey(s => s.Id);
        b.Property(s => s.Name).HasMaxLength(300).IsRequired();
        b.Property(s => s.Description).IsRequired();
        b.Property(s => s.CoverImageUrl).HasMaxLength(500);
        b.Property(s => s.PosterUrl).HasMaxLength(500);
        b.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(s => s.Format).HasConversion<string>().HasMaxLength(20);
        b.Property(s => s.PlaybackMode).HasConversion<string>().HasMaxLength(10)
            .HasDefaultValue(LivestreamPlaybackMode.TwoD);
        b.Property(s => s.Status).HasDefaultValue(LoungeShowStatus.Draft);
        b.Property(s => s.RefundPercentage).HasPrecision(5, 2);
        b.Property(s => s.CancellationAllowed).HasDefaultValue(true);
        b.Property(s => s.IsPublic).HasDefaultValue(true);
        b.Property(s => s.PosterByAi).HasDefaultValue(false);
        b.Property(s => s.LegalApprovalReference).HasMaxLength(500);
        b.Property(s => s.VcpmcRoyaltyReference).HasMaxLength(500);

        b.HasIndex(s => new { s.LoungeId, s.Status });
        b.HasIndex(s => s.ScheduledStart);

        b.HasOne(s => s.Lounge)
            .WithMany(l => l.LoungeShows)
            .HasForeignKey(s => s.LoungeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(s => s.Category)
            .WithMany(c => c.Shows)
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(s => s.LegalApprovalConfirmedByAdmin)
            .WithMany()
            .HasForeignKey(s => s.LegalApprovalConfirmedByAdminId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

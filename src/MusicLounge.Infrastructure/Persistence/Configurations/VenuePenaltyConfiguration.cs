using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class VenuePenaltyConfiguration : IEntityTypeConfiguration<VenuePenalty>
{
    public void Configure(EntityTypeBuilder<VenuePenalty> b)
    {
        b.ToTable("venue_penalties");
        b.HasKey(x => x.Id);
        b.Property(x => x.PenaltyType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.EvidenceRef).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.AppealReason).HasMaxLength(1000);
        b.Property(x => x.AppealResult).HasMaxLength(255);
        b.Property(x => x.CompensationNote).HasMaxLength(500);

        b.HasIndex(x => new { x.LoungeId, x.Status });

        b.HasOne(x => x.Lounge)
            .WithMany()
            .HasForeignKey(x => x.LoungeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.IssuedByUser)
            .WithMany()
            .HasForeignKey(x => x.IssuedBy)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ReviewedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReviewedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

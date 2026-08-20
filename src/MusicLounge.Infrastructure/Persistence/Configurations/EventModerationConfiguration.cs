using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class EventModerationConfiguration : IEntityTypeConfiguration<EventModeration>
{
    public void Configure(EntityTypeBuilder<EventModeration> b)
    {
        b.ToTable("event_moderations");
        b.HasKey(m => m.Id);

        b.Property(m => m.TargetType).HasConversion<string>().HasMaxLength(20);
        b.Property(m => m.FlagReason).HasMaxLength(1000);
        b.Property(m => m.RiskLevel).HasConversion<string>().HasMaxLength(20);
        b.Property(m => m.AiRecommendation).HasConversion<string>().HasMaxLength(30);
        b.Property(m => m.AdminDecision).HasConversion<string>().HasMaxLength(20);
        b.Property(m => m.ReviewNote).HasMaxLength(1000);

        b.HasIndex(m => new { m.TargetType, m.TargetId });
        b.HasIndex(m => m.AdminDecision);

        b.HasOne(m => m.Admin)
            .WithMany()
            .HasForeignKey(m => m.AdminId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

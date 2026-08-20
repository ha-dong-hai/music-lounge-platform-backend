using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class AiRecommendationConfiguration : IEntityTypeConfiguration<AiRecommendation>
{
    public void Configure(EntityTypeBuilder<AiRecommendation> b)
    {
        b.ToTable("ai_recommendations");
        b.HasKey(r => r.Id);
        b.Property(r => r.Algorithm).HasMaxLength(100);
        b.Property(r => r.Reason).HasMaxLength(500);
        b.HasIndex(r => new { r.UserId, r.LoungeShowId }).IsUnique();
        b.HasIndex(r => new { r.UserId, r.ExpiresAt });

        b.HasOne(r => r.User)
            .WithMany(u => u.AiRecommendations)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(r => r.LoungeShow)
            .WithMany(s => s.AiRecommendations)
            .HasForeignKey(r => r.LoungeShowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

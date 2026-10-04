using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LoungeShowRatingConfiguration : IEntityTypeConfiguration<LoungeShowRating>
{
    public void Configure(EntityTypeBuilder<LoungeShowRating> b)
    {
        b.ToTable("lounge_show_ratings");
        b.HasKey(r => r.Id);
        b.Property(r => r.Score).IsRequired();
        b.Property(r => r.Comment).HasMaxLength(1000);
        b.Property(r => r.IsRemoved).HasDefaultValue(false);
        b.Property(r => r.RemovedReason).HasMaxLength(500);
        // MLACP-574: cung do dai / kieu luu voi EventModeration (RiskLevel luu TEN enum).
        b.Property(r => r.AiRiskLevel).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.AiFlagReason).HasMaxLength(1000);
        b.Ignore(r => r.PublicComment);
        b.HasIndex(r => new { r.UserId, r.LoungeShowId }).IsUnique();

        b.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(r => r.LoungeShow)
            .WithMany(s => s.Ratings)
            .HasForeignKey(r => r.LoungeShowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

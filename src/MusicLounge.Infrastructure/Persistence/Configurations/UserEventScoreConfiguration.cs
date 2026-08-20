using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class UserEventScoreConfiguration : IEntityTypeConfiguration<UserEventScore>
{
    public void Configure(EntityTypeBuilder<UserEventScore> b)
    {
        b.ToTable("user_event_scores");
        b.HasKey(x => new { x.UserId, x.ShowId });   // composite PK — no surrogate key needed
        b.Property(x => x.Score).HasPrecision(8, 6);
        b.Property(x => x.Breakdown).HasMaxLength(1000);   // JSON

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Show)
            .WithMany()
            .HasForeignKey(x => x.ShowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

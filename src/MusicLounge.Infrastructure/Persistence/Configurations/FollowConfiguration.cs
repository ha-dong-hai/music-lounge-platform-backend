using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> b)
    {
        b.ToTable("follows");
        b.HasKey(f => f.Id);

        b.HasIndex(f => new { f.UserId, f.LoungeId }).IsUnique();
        b.HasIndex(f => f.LoungeId);

        b.HasOne(f => f.User)
            .WithMany(u => u.Follows)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(f => f.Lounge)
            .WithMany(l => l.Follows)
            .HasForeignKey(f => f.LoungeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

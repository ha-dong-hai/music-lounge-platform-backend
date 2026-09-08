using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> b)
    {
        b.ToTable("device_tokens");
        b.HasKey(d => d.Id);

        b.Property(d => d.Token).IsRequired().HasMaxLength(255);
        b.Property(d => d.Platform).HasMaxLength(20);

        // One row per physical device registration — re-registering the same token (app reopened,
        // token refreshed by FCM but unchanged) upserts in place instead of piling up duplicates
        // that would each receive their own push.
        b.HasIndex(d => d.Token).IsUnique();
        b.HasIndex(d => d.UserId);

        b.HasOne(d => d.User)
            .WithMany(u => u.DeviceTokens)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

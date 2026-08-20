using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LoungeImageConfiguration : IEntityTypeConfiguration<LoungeImage>
{
    public void Configure(EntityTypeBuilder<LoungeImage> b)
    {
        b.ToTable("lounge_images");
        b.HasKey(x => x.Id);
        b.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();
        b.Property(x => x.Caption).HasMaxLength(255);
        b.Property(x => x.DisplayOrder).HasDefaultValue(0);
        b.Property(x => x.IsPrimary).HasDefaultValue(false);

        // Only one primary image per lounge
        b.HasIndex(x => new { x.LoungeId, x.IsPrimary })
            .HasFilter("[IsPrimary] = 1")
            .IsUnique();

        b.HasOne(x => x.Lounge)
            .WithMany()
            .HasForeignKey(x => x.LoungeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

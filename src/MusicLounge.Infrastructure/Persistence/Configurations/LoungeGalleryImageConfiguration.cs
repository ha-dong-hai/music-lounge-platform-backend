using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LoungeGalleryImageConfiguration : IEntityTypeConfiguration<LoungeGalleryImage>
{
    public void Configure(EntityTypeBuilder<LoungeGalleryImage> b)
    {
        b.ToTable("lounge_gallery_images");
        b.HasKey(x => x.Id);
        b.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();
        b.Property(x => x.Caption).HasMaxLength(255);

        b.HasOne(x => x.Lounge)
            .WithMany()
            .HasForeignKey(x => x.LoungeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.LoungeId);
    }
}

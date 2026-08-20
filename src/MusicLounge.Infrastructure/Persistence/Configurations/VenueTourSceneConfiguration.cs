using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class VenueTourSceneConfiguration : IEntityTypeConfiguration<VenueTourScene>
{
    public void Configure(EntityTypeBuilder<VenueTourScene> b)
    {
        b.ToTable("venue_tour_scenes");
        b.HasKey(x => x.Id);
        b.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100);

        b.HasOne(x => x.Lounge)
            .WithMany()
            .HasForeignKey(x => x.LoungeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.LoungeId);
    }
}

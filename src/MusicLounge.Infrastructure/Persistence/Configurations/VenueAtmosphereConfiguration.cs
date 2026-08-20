using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class VenueAtmosphereConfiguration : IEntityTypeConfiguration<VenueAtmosphere>
{
    public void Configure(EntityTypeBuilder<VenueAtmosphere> b)
    {
        b.ToTable("venue_atmospheres");
        b.HasKey(a => a.Id);
        b.Property(a => a.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(a => a.Name).IsUnique();
    }
}

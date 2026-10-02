using MusicLounge.Domain.Common;
﻿using Microsoft.EntityFrameworkCore;
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

        // MLACP-14: danh mục phong cách không gian mặc định cho form tạo buổi diễn.
        b.HasData(
            new VenueAtmosphere { Id = OrderedGuid.FromLegacy("venue_atmospheres", 1), Name = "Ấm cúng" },
            new VenueAtmosphere { Id = OrderedGuid.FromLegacy("venue_atmospheres", 2), Name = "Sang trọng" },
            new VenueAtmosphere { Id = OrderedGuid.FromLegacy("venue_atmospheres", 3), Name = "Mộc mạc" },
            new VenueAtmosphere { Id = OrderedGuid.FromLegacy("venue_atmospheres", 4), Name = "Nghệ thuật" },
            new VenueAtmosphere { Id = OrderedGuid.FromLegacy("venue_atmospheres", 5), Name = "Hiện đại" }
        );
    }
}

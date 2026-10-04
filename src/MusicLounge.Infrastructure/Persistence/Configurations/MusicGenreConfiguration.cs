using MusicLounge.Domain.Common;
﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class MusicGenreConfiguration : IEntityTypeConfiguration<MusicGenre>
{
    public void Configure(EntityTypeBuilder<MusicGenre> b)
    {
        b.ToTable("music_genres");
        b.HasKey(g => g.Id);
        b.Property(g => g.Name).HasMaxLength(100).IsRequired();
        b.Property(g => g.NameEn).HasMaxLength(100);
        b.Property(g => g.ImageUrl).HasMaxLength(500);
        b.HasIndex(g => g.Name).IsUnique();

        // MLACP-14: danh mục thể loại nhạc mặc định cho form tạo buổi diễn.
        b.HasData(
            new MusicGenre { Id = OrderedGuid.FromLegacy("music_genres", 1), Name = "Jazz", NameEn = "Jazz" },
            new MusicGenre { Id = OrderedGuid.FromLegacy("music_genres", 2), Name = "Acoustic", NameEn = "Acoustic" },
            new MusicGenre { Id = OrderedGuid.FromLegacy("music_genres", 3), Name = "Ballad", NameEn = "Ballad" },
            new MusicGenre { Id = OrderedGuid.FromLegacy("music_genres", 4), Name = "Bolero", NameEn = "Bolero" },
            new MusicGenre { Id = OrderedGuid.FromLegacy("music_genres", 5), Name = "Pop", NameEn = "Pop" },
            new MusicGenre { Id = OrderedGuid.FromLegacy("music_genres", 6), Name = "Trữ tình" },
            new MusicGenre { Id = OrderedGuid.FromLegacy("music_genres", 7), Name = "R&B", NameEn = "R&B" },
            new MusicGenre { Id = OrderedGuid.FromLegacy("music_genres", 8), Name = "Cổ điển", NameEn = "Classical" }
        );
    }
}

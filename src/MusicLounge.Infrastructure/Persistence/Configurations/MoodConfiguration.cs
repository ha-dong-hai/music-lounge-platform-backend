using MusicLounge.Domain.Common;
﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class MoodConfiguration : IEntityTypeConfiguration<Mood>
{
    public void Configure(EntityTypeBuilder<Mood> b)
    {
        b.ToTable("moods");
        b.HasKey(m => m.Id);
        b.Property(m => m.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(m => m.Name).IsUnique();

        // MLACP-14: danh mục dòng nhạc/cảm xúc mặc định cho form tạo buổi diễn.
        b.HasData(
            new Mood { Id = OrderedGuid.FromLegacy("moods", 1), Name = "Hoài niệm" },
            new Mood { Id = OrderedGuid.FromLegacy("moods", 2), Name = "Tiền chiến" },
            new Mood { Id = OrderedGuid.FromLegacy("moods", 3), Name = "Lãng mạn" },
            new Mood { Id = OrderedGuid.FromLegacy("moods", 4), Name = "Chill" },
            new Mood { Id = OrderedGuid.FromLegacy("moods", 5), Name = "Sôi động" },
            new Mood { Id = OrderedGuid.FromLegacy("moods", 6), Name = "Nhẹ nhàng" }
        );
    }
}

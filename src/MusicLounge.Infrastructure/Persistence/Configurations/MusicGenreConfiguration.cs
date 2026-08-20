using Microsoft.EntityFrameworkCore;
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
        b.HasIndex(g => g.Name).IsUnique();
    }
}

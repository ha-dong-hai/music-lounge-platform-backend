using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class PerformerGenreConfiguration : IEntityTypeConfiguration<PerformerGenre>
{
    public void Configure(EntityTypeBuilder<PerformerGenre> b)
    {
        b.ToTable("performer_genres");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.PerformerId, x.GenreId }).IsUnique();

        b.HasOne(x => x.Performer)
            .WithMany(p => p.Genres)
            .HasForeignKey(x => x.PerformerId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Genre)
            .WithMany(g => g.PerformerGenres)
            .HasForeignKey(x => x.GenreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

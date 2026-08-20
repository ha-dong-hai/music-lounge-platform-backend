using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LoungeShowGenreConfiguration : IEntityTypeConfiguration<LoungeShowGenre>
{
    public void Configure(EntityTypeBuilder<LoungeShowGenre> b)
    {
        b.ToTable("lounge_show_genres");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.LoungeShowId, x.GenreId }).IsUnique();

        b.HasOne(x => x.LoungeShow)
            .WithMany(s => s.Genres)
            .HasForeignKey(x => x.LoungeShowId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Genre)
            .WithMany(g => g.LoungeShowGenres)
            .HasForeignKey(x => x.GenreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

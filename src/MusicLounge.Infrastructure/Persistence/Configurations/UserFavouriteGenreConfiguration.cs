using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class UserFavouriteGenreConfiguration : IEntityTypeConfiguration<UserFavouriteGenre>
{
    public void Configure(EntityTypeBuilder<UserFavouriteGenre> b)
    {
        b.ToTable("user_favourite_genres");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.GenreId }).IsUnique();

        b.HasOne(x => x.User)
            .WithMany(u => u.FavouriteGenres)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Genre)
            .WithMany(g => g.UserFavourites)
            .HasForeignKey(x => x.GenreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

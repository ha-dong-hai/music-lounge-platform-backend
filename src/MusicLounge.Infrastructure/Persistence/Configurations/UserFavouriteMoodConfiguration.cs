using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class UserFavouriteMoodConfiguration : IEntityTypeConfiguration<UserFavouriteMood>
{
    public void Configure(EntityTypeBuilder<UserFavouriteMood> b)
    {
        b.ToTable("user_favourite_moods");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.MoodId }).IsUnique();

        b.HasOne(x => x.User)
            .WithMany(u => u.FavouriteMoods)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Mood)
            .WithMany(m => m.UserFavourites)
            .HasForeignKey(x => x.MoodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

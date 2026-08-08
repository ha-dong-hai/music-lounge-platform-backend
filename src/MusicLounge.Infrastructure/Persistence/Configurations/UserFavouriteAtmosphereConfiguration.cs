using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class UserFavouriteAtmosphereConfiguration
    : IEntityTypeConfiguration<UserFavouriteAtmosphere>
{
    public void Configure(EntityTypeBuilder<UserFavouriteAtmosphere> b)
    {
        b.ToTable("user_favourite_atmospheres");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.AtmosphereId }).IsUnique();

        b.HasOne(x => x.User)
            .WithMany(u => u.FavouriteAtmospheres)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Atmosphere)
            .WithMany(a => a.UserFavourites)
            .HasForeignKey(x => x.AtmosphereId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

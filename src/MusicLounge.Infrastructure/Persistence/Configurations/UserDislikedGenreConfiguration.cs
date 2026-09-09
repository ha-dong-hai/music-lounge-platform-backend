using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class UserDislikedGenreConfiguration : IEntityTypeConfiguration<UserDislikedGenre>
{
    public void Configure(EntityTypeBuilder<UserDislikedGenre> b)
    {
        b.ToTable("user_disliked_genres");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.GenreId }).IsUnique();

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict giong UserFavouriteGenre: xoa mot the loai khoi danh muc khong duoc lang le keo
        // theo lua chon cua nguoi dung.
        b.HasOne(x => x.Genre)
            .WithMany()
            .HasForeignKey(x => x.GenreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

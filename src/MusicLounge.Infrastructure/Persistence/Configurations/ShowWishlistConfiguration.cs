using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class ShowWishlistConfiguration : IEntityTypeConfiguration<ShowWishlist>
{
    public void Configure(EntityTypeBuilder<ShowWishlist> b)
    {
        b.ToTable("show_wishlists");
        b.HasKey(w => w.Id);
        b.HasIndex(w => new { w.UserId, w.LoungeShowId }).IsUnique();

        b.HasOne(w => w.User)
            .WithMany(u => u.Wishlists)
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(w => w.LoungeShow)
            .WithMany(s => s.Wishlists)
            .HasForeignKey(w => w.LoungeShowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

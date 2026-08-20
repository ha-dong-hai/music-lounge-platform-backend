using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class FnbMenuItemConfiguration : IEntityTypeConfiguration<FnbMenuItem>
{
    public void Configure(EntityTypeBuilder<FnbMenuItem> b)
    {
        b.ToTable("fnb_menu_items");
        b.HasKey(x => x.Id);
        b.Property(x => x.Category).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(255).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Price).HasPrecision(15, 2);
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.IsAvailable).HasDefaultValue(true);
        b.Property(x => x.DisplayOrder).HasDefaultValue(0);
    }
}

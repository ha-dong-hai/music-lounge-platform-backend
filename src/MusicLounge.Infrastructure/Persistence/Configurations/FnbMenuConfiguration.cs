using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class FnbMenuConfiguration : IEntityTypeConfiguration<FnbMenu>
{
    public void Configure(EntityTypeBuilder<FnbMenu> b)
    {
        b.ToTable("fnb_menus");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(255).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.DisplayOrder).HasDefaultValue(0);

        b.HasOne(x => x.Lounge)
            .WithMany(x => x.Menus)
            .HasForeignKey(x => x.LoungeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Items)
            .WithOne(x => x.Menu)
            .HasForeignKey(x => x.MenuId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

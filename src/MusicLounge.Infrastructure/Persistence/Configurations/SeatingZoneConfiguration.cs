using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class SeatingZoneConfiguration : IEntityTypeConfiguration<SeatingZone>
{
    public void Configure(EntityTypeBuilder<SeatingZone> b)
    {
        b.ToTable("seating_zones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.DisplayOrder).HasDefaultValue(0);
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.LayoutColor).HasMaxLength(9); // #RRGGBBAA toi da

        b.HasOne(x => x.Lounge)
            .WithMany()
            .HasForeignKey(x => x.LoungeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

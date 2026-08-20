using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class TicketTierConfiguration : IEntityTypeConfiguration<TicketTier>
{
    public void Configure(EntityTypeBuilder<TicketTier> b)
    {
        b.ToTable("ticket_tiers");
        b.HasKey(t => t.Id);
        b.Property(t => t.Name).HasMaxLength(100).IsRequired();
        b.Property(t => t.Description).HasMaxLength(500);
        b.Property(t => t.AccessType).HasConversion<string>().HasMaxLength(20);
        b.Property(t => t.TotalCapacity);

        b.HasOne(t => t.LoungeShow)
            .WithMany(s => s.TicketTiers)
            .HasForeignKey(t => t.LoungeShowId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(t => t.Zone)
            .WithMany()
            .HasForeignKey(t => t.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class TicketHoldConfiguration : IEntityTypeConfiguration<TicketHold>
{
    public void Configure(EntityTypeBuilder<TicketHold> b)
    {
        b.ToTable("ticket_holds");
        b.HasKey(h => h.Id);

        b.HasOne(h => h.User)
            .WithMany(u => u.TicketHolds)
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(h => h.Price)
            .WithMany(p => p.Holds)
            .HasForeignKey(h => h.PriceId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Property(h => h.IsReleased).HasDefaultValue(false);

        b.HasIndex(h => h.ExpiresAt);
        b.HasIndex(h => new { h.PriceId, h.IsReleased, h.ExpiresAt });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class TicketPriceConfiguration : IEntityTypeConfiguration<TicketPrice>
{
    public void Configure(EntityTypeBuilder<TicketPrice> b)
    {
        b.ToTable("ticket_prices");
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).HasMaxLength(100).IsRequired();
        b.Property(p => p.Description).HasMaxLength(500);
        b.Property(p => p.Price).HasPrecision(15, 2);
        b.Property(p => p.Sold).HasDefaultValue(0);
        b.Property(p => p.IsActive).HasDefaultValue(true);
        b.Property(p => p.PurchaseChannel).HasConversion<string>().HasMaxLength(20);

        b.HasOne(p => p.Tier)
            .WithMany(t => t.Prices)
            .HasForeignKey(p => p.TierId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

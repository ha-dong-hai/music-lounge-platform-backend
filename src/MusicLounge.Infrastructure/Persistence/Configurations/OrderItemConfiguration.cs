using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("fnb_order_items");
        b.HasKey(x => x.Id);
        b.Property(x => x.UnitPrice).HasPrecision(15, 2);   // D12: snapshot at order time
        b.Property(x => x.Note).HasMaxLength(255);
        b.Property(x => x.Cancelled).HasDefaultValue(false);

        b.HasOne(x => x.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(x => x.FnbOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.MenuItem)
            .WithMany(m => m.OrderItems)
            .HasForeignKey(x => x.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

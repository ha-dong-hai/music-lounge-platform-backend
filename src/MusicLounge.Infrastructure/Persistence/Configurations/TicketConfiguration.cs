using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.ToTable("tickets");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        b.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        b.Property(t => t.PurchaseChannel)
            .HasConversion<string>()
            .HasMaxLength(20);

        b.Property(t => t.QrCode).HasMaxLength(64);

        b.HasOne(t => t.Buyer)
            .WithMany(u => u.Tickets)
            .HasForeignKey(t => t.BuyerId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(t => t.Price)
            .WithMany(p => p.Tickets)
            .HasForeignKey(t => t.PriceId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(t => t.Tier)
            .WithMany()
            .HasForeignKey(t => t.TierId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(t => t.Show)
            .WithMany(s => s.Tickets)
            .HasForeignKey(t => t.ShowId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(t => t.Payment)
            .WithMany(p => p.Tickets)
            .HasForeignKey(t => t.PaymentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Restrict (khong phai SetNull) vi tickets.BuyerId da SetNull->users; SQL Server khong cho
        // 2 duong cascade tu cung 1 bang toi cung 1 bang dich.
        b.HasOne(t => t.PendingTransferToUser)
            .WithMany()
            .HasForeignKey(t => t.PendingTransferToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(t => t.QrCode).IsUnique().HasFilter("[QrCode] IS NOT NULL");
        b.HasIndex(t => t.BuyerId);
        b.HasIndex(t => new { t.ShowId, t.Status });
        b.HasIndex(t => t.PaymentId);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments");
        b.HasKey(p => p.Id);

        b.Property(p => p.OrderId).HasMaxLength(40).IsRequired();
        b.Property(p => p.GrossAmount).HasPrecision(18, 2);
        b.Property(p => p.GatewayFee).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(p => p.PlatformFee).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(p => p.TaxWithheld).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(p => p.NetAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(p => p.Method).HasConversion<string>().HasMaxLength(20).HasDefaultValue(PaymentMethod.Gateway);
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.SettlementStatus).HasConversion<string>().HasMaxLength(30).HasDefaultValue(PaymentSettlementStatus.NotApplicable);
        b.Property(p => p.IdempotencyKey).HasMaxLength(100);
        b.Property(p => p.TransactionId).HasMaxLength(100);
        b.Property(p => p.VnPayResponseCode).HasMaxLength(10);
        b.Property(p => p.ReferenceType).HasMaxLength(50).IsRequired();
        b.Property(p => p.ReferenceId).HasMaxLength(100).IsRequired();

        b.HasOne(p => p.Payer)
            .WithMany()
            .HasForeignKey(p => p.PayerId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(p => p.OrderId).IsUnique();
        b.HasIndex(p => p.Status);
        b.HasIndex(p => p.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        b.HasIndex(p => p.TransactionId).IsUnique().HasFilter("[TransactionId] IS NOT NULL");
    }
}

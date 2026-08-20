using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> b)
    {
        b.ToTable("settlements");
        b.HasKey(s => s.Id);

        b.Property(s => s.ReleaseType).HasConversion<string>().HasMaxLength(20);
        b.Property(s => s.GrossAmount).HasPrecision(15, 2);
        b.Property(s => s.PreRateApplied).HasPrecision(5, 4);
        b.Property(s => s.PostRateApplied).HasPrecision(5, 4);
        b.Property(s => s.NetAmount).HasPrecision(15, 2);
        b.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(s => s.LedgerJournalId).HasMaxLength(100);
        b.Property(s => s.PaymentReference).HasMaxLength(200);

        b.HasOne(s => s.Payment)
            .WithMany()
            .HasForeignKey(s => s.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(s => s.BankAccount)
            .WithMany()
            .HasForeignKey(s => s.BankAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(s => new { s.OwnerId, s.ReleaseType, s.Status });
        b.HasIndex(s => s.ScheduledAt);
    }
}

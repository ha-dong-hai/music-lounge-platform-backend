using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> b)
    {
        b.ToTable("ledger_entries");
        b.HasKey(e => e.Id);

        b.Property(e => e.JournalId).HasMaxLength(32).IsRequired();
        b.Property(e => e.Amount).HasPrecision(18, 2);
        b.Property(e => e.ReferenceType).HasMaxLength(50).IsRequired();
        b.Property(e => e.ReferenceId).HasMaxLength(255).IsRequired();
        b.Property(e => e.Description).HasMaxLength(500);

        // Append-only — no UpdatedAt ever written
        b.Property(e => e.CreatedAt).IsRequired();

        b.HasOne(e => e.Account)
            .WithMany(a => a.Entries)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(e => e.Payment)
            .WithMany(p => p.LedgerEntries)
            .HasForeignKey(e => e.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(e => e.JournalId);
        b.HasIndex(e => e.PaymentId);
        b.HasIndex(e => new { e.ReferenceType, e.ReferenceId });
    }
}

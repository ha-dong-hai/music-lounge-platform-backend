using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.ToTable("ledger_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(20).IsRequired();

        // OwnerId is null for system accounts (Gateway/Platform/Tax). A single composite unique
        // index on (OwnerType, OwnerId) does NOT block duplicates among the null-OwnerId rows —
        // SQL Server treats every NULL as distinct for uniqueness purposes — so two concurrent
        // GetOrCreateAccountAsync calls (LedgerService, check-then-act with no lock) could each
        // insert their own "Platform" row, splitting the ledger. Split into two filtered indexes:
        // one for real owners (OwnerId present), one per system account type (OwnerId null).
        b.HasIndex(x => new { x.OwnerType, x.OwnerId }).IsUnique().HasFilter("[OwnerId] IS NOT NULL");
        b.HasIndex(x => x.OwnerType).IsUnique().HasFilter("[OwnerId] IS NULL");
    }
}

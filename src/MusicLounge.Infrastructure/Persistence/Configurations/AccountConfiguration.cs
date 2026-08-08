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
        // OwnerId is null for system accounts (Gateway/Platform/Tax)
        b.HasIndex(x => new { x.OwnerType, x.OwnerId }).IsUnique();
    }
}

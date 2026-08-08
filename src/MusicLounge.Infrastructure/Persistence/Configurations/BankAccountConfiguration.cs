using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> b)
    {
        b.ToTable("bank_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.BankName).HasMaxLength(255).IsRequired();
        b.Property(x => x.AccountNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.AccountHolder).HasMaxLength(255).IsRequired();
        b.Property(x => x.IsDefault).HasDefaultValue(true);
        b.Property(x => x.IsVerified).HasDefaultValue(false);
        // Polymorphic — no FK constraint. OwnerId refers to lounge.id or performer.id.
        b.HasIndex(x => new { x.OwnerType, x.OwnerId });
    }
}

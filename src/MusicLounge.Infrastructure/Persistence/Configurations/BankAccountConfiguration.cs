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
        // Ciphertext (IPiiEncryptionService), much longer than the raw account number — Data
        // Protection's Protect() output carries key-ring metadata overhead. Same treatment/reasoning
        // as User.CitizenCardNumber in UserConfiguration.cs.
        b.Property(x => x.AccountNumber).HasMaxLength(500).IsRequired();
        b.Property(x => x.AccountHolder).HasMaxLength(255).IsRequired();
        b.Property(x => x.IsDefault).HasDefaultValue(true);
        b.Property(x => x.IsVerified).HasDefaultValue(false);
        // Polymorphic — no FK constraint. OwnerId refers to lounge.id or performer.id.
        b.HasIndex(x => new { x.OwnerType, x.OwnerId });
        // App-level "unset others" in Create/UpdateBankAccountCommandHandler is read-then-write with
        // no lock — two concurrent "set as default" requests can both pass before either writes,
        // leaving two IsDefault=true rows for the same owner. Same filtered-unique-index backstop
        // pattern as OwnerSubscriptionConfiguration/LoungeStaffConfiguration elsewhere in this
        // codebase: the second concurrent INSERT/UPDATE throws, GlobalExceptionHandler maps it to a
        // clean 409 instead of silently allowing two defaults (which would make the settlement/
        // donation payout wiring's "pick the default account" lookup pick one arbitrarily).
        b.HasIndex(x => new { x.OwnerType, x.OwnerId }).IsUnique().HasFilter("[IsDefault] = 1");
    }
}

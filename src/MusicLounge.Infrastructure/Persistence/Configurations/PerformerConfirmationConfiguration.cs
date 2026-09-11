using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class PerformerConfirmationConfiguration : IEntityTypeConfiguration<PerformerConfirmation>
{
    public void Configure(EntityTypeBuilder<PerformerConfirmation> b)
    {
        b.ToTable("performer_confirmations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.BankAccountFingerprint).HasMaxLength(64);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.SentToEmail).HasMaxLength(255).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);

        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.PerformerId);

        b.HasOne<Performer>()
            .WithMany()
            .HasForeignKey(x => x.PerformerId)
            .OnDelete(DeleteBehavior.Restrict);

        // NoAction: bản ghi xác nhận là bằng chứng — không được mất theo tài khoản hay khoản donate.
        b.HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(x => x.BankAccountId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<Donation>()
            .WithMany()
            .HasForeignKey(x => x.DonationId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

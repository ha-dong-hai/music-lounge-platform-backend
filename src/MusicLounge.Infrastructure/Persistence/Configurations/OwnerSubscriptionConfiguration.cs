using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class OwnerSubscriptionConfiguration : IEntityTypeConfiguration<OwnerSubscription>
{
    public void Configure(EntityTypeBuilder<OwnerSubscription> b)
    {
        b.ToTable("owner_subscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.AutoRenew).HasDefaultValue(false);

        b.HasIndex(x => new { x.OwnerId, x.Status });

        // SubscribeToPackageCommandHandler's "chưa có subscription Active" check is a read-then-write
        // with no lock, and the actual INSERT happens later in a DIFFERENT command
        // (ProcessSubscriptionPaymentCommandHandler, once VNPay confirms) — 2 near-simultaneous
        // subscribe attempts can both pass the app-level check before either payment confirms.
        // Filtered unique index is the final DB-level backstop, same pattern as
        // LoungeStaffConfiguration's per-user active-assignment constraint: the 2nd concurrent
        // INSERT throws DbUpdateException, which GlobalExceptionHandler maps to a clean 409
        // instead of silently creating 2 active subscriptions for the same owner.
        // No N'...' prefix — unlike free-text search, this literal is a fixed ASCII enum value
        // ("Active"), and the SQLite provider used in tests doesn't understand the N-string
        // syntax at all (breaks EnsureCreatedAsync outright, not just a Unicode edge case).
        b.HasIndex(x => x.OwnerId).IsUnique().HasFilter("[Status] = 'Active'");

        b.HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Package)
            .WithMany(p => p.Subscriptions)
            .HasForeignKey(x => x.PackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

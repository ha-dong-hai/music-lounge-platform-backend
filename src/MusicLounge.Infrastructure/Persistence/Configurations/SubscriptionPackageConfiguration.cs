using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionPackageConfiguration : IEntityTypeConfiguration<SubscriptionPackage>
{
    public void Configure(EntityTypeBuilder<SubscriptionPackage> b)
    {
        b.ToTable("subscription_packages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Price).HasPrecision(15, 2);
        b.Property(x => x.BillingCycle).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.HasAiPoster).HasDefaultValue(false);
        b.Property(x => x.IsActive).HasDefaultValue(true);
    }
}

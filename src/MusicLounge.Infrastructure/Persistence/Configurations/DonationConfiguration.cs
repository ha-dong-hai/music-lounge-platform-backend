using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class DonationConfiguration : IEntityTypeConfiguration<Donation>
{
    public void Configure(EntityTypeBuilder<Donation> b)
    {
        b.ToTable("donations");
        b.HasKey(d => d.Id);

        b.Property(d => d.Gross).HasColumnType("decimal(15,2)");
        b.Property(d => d.Net).HasColumnType("decimal(15,2)");
        b.Property(d => d.PerformerShareRateSnapshot).HasPrecision(5, 4);
        b.Property(d => d.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(d => d.PaymentRef).HasMaxLength(255);
        b.Property(d => d.PaymentEvidenceUrl).HasMaxLength(500);
        b.Property(d => d.DisplayName).HasMaxLength(255);
        b.Property(d => d.GatewayRef).HasMaxLength(255);
        b.HasOne(d => d.BankAccount)
            .WithMany()
            .HasForeignKey(d => d.BankAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(d => d.Donor)
            .WithMany()
            .HasForeignKey(d => d.DonorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(d => d.Performance)
            .WithMany()
            .HasForeignKey(d => d.PerformanceId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(d => d.PerformanceId);
        b.HasIndex(d => d.Status);
        b.HasIndex(d => d.DonorUserId);
    }
}

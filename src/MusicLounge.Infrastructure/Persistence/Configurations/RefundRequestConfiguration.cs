using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class RefundRequestConfiguration : IEntityTypeConfiguration<RefundRequest>
{
    public void Configure(EntityTypeBuilder<RefundRequest> b)
    {
        b.ToTable("refund_requests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.AmountRequested).HasPrecision(18, 2);
        b.Property(x => x.AmountApproved).HasPrecision(18, 2);
        b.Property(x => x.RefundPercentage).HasPrecision(5, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);

        b.HasIndex(x => new { x.PaymentId, x.Status });

        b.HasOne(x => x.Payment)
            .WithMany()
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        // BVDLCN: SET NULL when requester deletes account
        b.HasOne(x => x.Requester)
            .WithMany()
            .HasForeignKey(x => x.RequestedBy)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Processor)
            .WithMany()
            .HasForeignKey(x => x.ProcessedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

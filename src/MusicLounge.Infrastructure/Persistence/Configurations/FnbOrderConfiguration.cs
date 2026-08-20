using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class FnbOrderConfiguration : IEntityTypeConfiguration<FnbOrder>
{
    public void Configure(EntityTypeBuilder<FnbOrder> b)
    {
        b.ToTable("fnb_orders");
        b.HasKey(x => x.Id);
        b.Property(x => x.TableNote).HasMaxLength(100);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.TotalAmount).HasPrecision(15, 2).HasDefaultValue(0m);
        b.Property(x => x.Note).HasMaxLength(500);

        b.HasIndex(x => new { x.LoungeId, x.Status });

        b.HasOne(x => x.Lounge)
            .WithMany()
            .HasForeignKey(x => x.LoungeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Show)
            .WithMany()
            .HasForeignKey(x => x.ShowId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.AudienceUser)
            .WithMany()
            .HasForeignKey(x => x.AudienceUserId)
            .OnDelete(DeleteBehavior.SetNull);   // BVDLCN

        b.HasOne(x => x.Staff)
            .WithMany()
            .HasForeignKey(x => x.StaffId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Zone)
            .WithMany()
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

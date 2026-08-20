using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> b)
    {
        b.ToTable("complaints");
        b.HasKey(x => x.Id);
        b.Property(x => x.TargetType).HasMaxLength(50).IsRequired();
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        b.Property(x => x.EvidenceUrls).HasMaxLength(2000);   // JSON array
        b.Property(x => x.ContactPhone).HasMaxLength(20);      // D17: guest reporter
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Resolution).HasMaxLength(2000);
        b.Property(x => x.ResolvedAction).HasConversion<string>().HasMaxLength(50);

        b.HasIndex(x => new { x.Status, x.CreatedAt });

        // BVDLCN: SET NULL when complainant deletes account
        b.HasOne(x => x.Complainant)
            .WithMany()
            .HasForeignKey(x => x.ComplainantUserId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Admin)
            .WithMany()
            .HasForeignKey(x => x.AdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

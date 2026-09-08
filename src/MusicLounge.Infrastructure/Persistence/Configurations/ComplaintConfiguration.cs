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
        b.Property(x => x.LookupReference).HasMaxLength(32);
        // Loc theo NOT NULL: chi khieu nai cua khach vang lai moi co ma tra cuu, va SQL Server coi
        // moi NULL la khac nhau nen mot unique index khong loc se van cho trung — nhung index loc
        // moi bat duoc dung y do la "hai khieu nai khong duoc trung ma".
        b.HasIndex(x => x.LookupReference).IsUnique().HasFilter("[LookupReference] IS NOT NULL");

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

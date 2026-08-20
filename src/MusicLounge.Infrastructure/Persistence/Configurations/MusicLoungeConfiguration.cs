using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Enums;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class MusicLoungeConfiguration : IEntityTypeConfiguration<MusicLoungeVenue>
{
    public void Configure(EntityTypeBuilder<MusicLoungeVenue> b)
    {
        b.ToTable("music_lounges");
        b.HasKey(l => l.Id);
        b.Property(l => l.Name).HasMaxLength(200).IsRequired();
        b.Property(l => l.Description).HasMaxLength(2000);
        b.Property(l => l.PrimaryImageUrl).HasMaxLength(500);
        b.Property(l => l.AreaLayoutImageUrl).HasMaxLength(500);
        b.Property(l => l.BusinessLicenseUrl).HasMaxLength(500);
        b.Property(l => l.Model3DUrl).HasMaxLength(500);
        b.Property(l => l.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(LoungeStatus.Pending);
        b.Property(l => l.ReputationScore).HasPrecision(3, 2).HasDefaultValue(0m);
        b.HasIndex(l => l.Status);

        b.HasOne(l => l.Atmosphere)
            .WithMany()
            .HasForeignKey(l => l.AtmosphereId)
            .OnDelete(DeleteBehavior.SetNull);

        // VenueAddress — Owned Value Object, map thành cột phẳng trong bảng Lounges
        b.OwnsOne(l => l.Address, addr =>
        {
            addr.Property(a => a.Street).HasMaxLength(300).IsRequired().HasColumnName("Address_Street");
            addr.Property(a => a.Ward).HasMaxLength(100).IsRequired().HasColumnName("Address_Ward");
            addr.Property(a => a.District).HasMaxLength(100).IsRequired().HasColumnName("Address_District");
            addr.Property(a => a.City).HasMaxLength(100).IsRequired().HasColumnName("Address_City");
            addr.Property(a => a.Latitude).HasColumnName("Address_Latitude");
            addr.Property(a => a.Longitude).HasColumnName("Address_Longitude");
            addr.Ignore(a => a.FullAddress); // computed property, không map vào DB
            addr.HasIndex(a => a.City);
            addr.HasIndex(a => new { a.City, a.District });
        });

        b.HasOne(l => l.Owner)
            .WithMany()
            .HasForeignKey(l => l.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

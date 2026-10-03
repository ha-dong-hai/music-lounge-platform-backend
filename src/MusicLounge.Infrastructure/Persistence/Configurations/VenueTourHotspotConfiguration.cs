using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class VenueTourHotspotConfiguration : IEntityTypeConfiguration<VenueTourHotspot>
{
    public void Configure(EntityTypeBuilder<VenueTourHotspot> b)
    {
        b.ToTable("venue_tour_hotspots");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Label).HasMaxLength(100);
        b.Property(x => x.InfoText).HasMaxLength(2000);

        b.HasOne(x => x.Scene)
            .WithMany(x => x.Hotspots)
            .HasForeignKey(x => x.SceneId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade — this and the FK above both point at VenueTourScene, and SQL
        // Server rejects a schema with two cascade paths into the same table (error 1785, hit
        // once before in this codebase on PhysicalTicketDetail's two staff FKs). Restrict also
        // means "delete a scene that's still someone else's navigation target" fails loudly at
        // the DB if the handler's own explicit cleanup (removing referencing hotspots first)
        // were ever skipped, rather than silently leaving a hotspot pointing at nothing.
        b.HasOne(x => x.TargetScene)
            .WithMany()
            .HasForeignKey(x => x.TargetSceneId)
            .OnDelete(DeleteBehavior.Restrict);

        // MLACP-555. Restrict: zones are never hard-deleted (DeactivateSeatingZone only clears IsActive),
        // and a Cascade here would be a second cascade path from music_lounges (via scenes and via
        // zones) — the same SQL Server error 1785 as above. A deactivated zone's hotspot stays in the
        // table; GetVenueTourQueryHandler hides it so the Audience never taps a zone that isn't sold.
        b.HasOne(x => x.Zone)
            .WithMany()
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.SceneId);
    }
}

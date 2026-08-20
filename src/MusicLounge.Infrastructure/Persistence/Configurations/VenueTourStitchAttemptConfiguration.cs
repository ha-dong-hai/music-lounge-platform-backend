using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class VenueTourStitchAttemptConfiguration : IEntityTypeConfiguration<VenueTourStitchAttempt>
{
    public void Configure(EntityTypeBuilder<VenueTourStitchAttempt> b)
    {
        b.ToTable("venue_tour_stitch_attempts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ErrorMessage).HasMaxLength(2000);

        b.HasOne(x => x.Lounge)
            .WithMany()
            .HasForeignKey(x => x.LoungeId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction, not SetNull/Cascade — VenueTourScene already cascades from MusicLounge, so a
        // second cascading path from MusicLounge through THIS FK (Lounge -> Scene -> this table)
        // would collide with the direct Lounge -> this table Cascade above (SQL Server error 1785,
        // the same "multiple cascade paths" issue hit before in this codebase on
        // PhysicalTicketDetail's two staff FKs). RemoveVenueTourSceneCommandHandler explicitly
        // nulls out any attempt row referencing the scene being deleted, before deleting it — same
        // explicit-cleanup pattern already used there for VenueTourHotspot.TargetSceneId.
        b.HasOne(x => x.ResultScene)
            .WithMany()
            .HasForeignKey(x => x.ResultSceneId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.LoungeId);
    }
}

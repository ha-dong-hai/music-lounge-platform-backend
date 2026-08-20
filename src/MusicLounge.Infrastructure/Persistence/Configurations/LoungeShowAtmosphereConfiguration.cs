using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LoungeShowAtmosphereConfiguration : IEntityTypeConfiguration<LoungeShowAtmosphere>
{
    public void Configure(EntityTypeBuilder<LoungeShowAtmosphere> b)
    {
        b.ToTable("lounge_show_atmospheres");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.LoungeShowId, x.AtmosphereId }).IsUnique();

        b.HasOne(x => x.LoungeShow)
            .WithMany(s => s.Atmospheres)
            .HasForeignKey(x => x.LoungeShowId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Atmosphere)
            .WithMany(a => a.LoungeShowAtmospheres)
            .HasForeignKey(x => x.AtmosphereId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

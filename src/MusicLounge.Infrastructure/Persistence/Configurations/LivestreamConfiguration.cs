using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

public sealed class LivestreamConfiguration : IEntityTypeConfiguration<Livestream>
{
    public void Configure(EntityTypeBuilder<Livestream> builder)
    {
        builder.ToTable("livestreams");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Provider).HasMaxLength(20);
        builder.Property(l => l.ProviderRef).HasMaxLength(100);
        builder.Property(l => l.RtmpUrl).HasMaxLength(500);
        builder.Property(l => l.StreamKey).HasMaxLength(200);
        builder.Property(l => l.HlsUrl).HasMaxLength(500);
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.IsFree).HasDefaultValue(true);
        builder.Property(l => l.ChatEnabled).HasDefaultValue(true);
        builder.Property(l => l.PeakViewerCount).HasDefaultValue(0);
        builder.Property(l => l.TotalViews).HasDefaultValue(0);
        builder.Property(l => l.RecordingUrl).HasMaxLength(500);
        builder.Property(l => l.TerminatedReason).HasMaxLength(1000);

        builder.HasOne(l => l.LoungeShow)
            .WithOne(ls => ls.Livestream)
            .HasForeignKey<Livestream>(l => l.LoungeShowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.TerminatedBy)
            .WithMany()
            .HasForeignKey(l => l.TerminatedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(l => l.LoungeShowId).IsUnique();
    }
}

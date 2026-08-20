using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class PerformanceConfiguration : IEntityTypeConfiguration<Performance>
{
    public void Configure(EntityTypeBuilder<Performance> b)
    {
        b.ToTable("performances");
        b.HasKey(p => p.Id);
        b.Property(p => p.Role).HasConversion<string>().HasMaxLength(20).HasDefaultValue(PerformerRole.Main);
        b.Property(p => p.AcceptsDonation).HasDefaultValue(true);
        b.HasIndex(p => new { p.LoungeShowId, p.PerformerId }).IsUnique();

        b.HasOne(p => p.LoungeShow)
            .WithMany(s => s.Performances)
            .HasForeignKey(p => p.LoungeShowId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(p => p.Performer)
            .WithMany(perf => perf.Performances)
            .HasForeignKey(p => p.PerformerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

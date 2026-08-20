using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LoungeShowMoodConfiguration : IEntityTypeConfiguration<LoungeShowMood>
{
    public void Configure(EntityTypeBuilder<LoungeShowMood> b)
    {
        b.ToTable("lounge_show_moods");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.LoungeShowId, x.MoodId }).IsUnique();

        b.HasOne(x => x.LoungeShow)
            .WithMany(s => s.Moods)
            .HasForeignKey(x => x.LoungeShowId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Mood)
            .WithMany(m => m.LoungeShowMoods)
            .HasForeignKey(x => x.MoodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

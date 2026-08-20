using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class EventCustomValueConfiguration : IEntityTypeConfiguration<EventCustomValue>
{
    public void Configure(EntityTypeBuilder<EventCustomValue> b)
    {
        b.ToTable("event_custom_values");
        b.HasKey(x => x.Id);
        b.Property(x => x.Value).HasMaxLength(500).IsRequired();

        b.HasIndex(x => new { x.ShowId, x.CriteriaId }).IsUnique();

        b.HasOne(x => x.Show)
            .WithMany()
            .HasForeignKey(x => x.ShowId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Criteria)
            .WithMany(c => c.EventValues)
            .HasForeignKey(x => x.CriteriaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

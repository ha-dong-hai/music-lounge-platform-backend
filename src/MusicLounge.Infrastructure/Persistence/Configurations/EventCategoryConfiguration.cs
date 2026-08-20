using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class EventCategoryConfiguration : IEntityTypeConfiguration<EventCategory>
{
    public void Configure(EntityTypeBuilder<EventCategory> b)
    {
        b.ToTable("event_categories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.IsActive).HasDefaultValue(true);
    }
}

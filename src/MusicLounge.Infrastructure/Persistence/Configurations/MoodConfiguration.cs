using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class MoodConfiguration : IEntityTypeConfiguration<Mood>
{
    public void Configure(EntityTypeBuilder<Mood> b)
    {
        b.ToTable("moods");
        b.HasKey(m => m.Id);
        b.Property(m => m.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(m => m.Name).IsUnique();
    }
}

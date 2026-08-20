using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class CustomCriteriaConfiguration : IEntityTypeConfiguration<CustomCriteria>
{
    public void Configure(EntityTypeBuilder<CustomCriteria> b)
    {
        b.ToTable("custom_criteria");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Key).HasMaxLength(100).IsRequired();
        b.Property(x => x.DataType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Options).HasMaxLength(1000);   // JSON
        b.Property(x => x.IsActive).HasDefaultValue(true);

        b.HasIndex(x => new { x.LoungeId, x.Key }).IsUnique();

        b.HasOne(x => x.Lounge)
            .WithMany()
            .HasForeignKey(x => x.LoungeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

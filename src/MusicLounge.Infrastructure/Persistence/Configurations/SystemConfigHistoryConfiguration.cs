using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class SystemConfigHistoryConfiguration : IEntityTypeConfiguration<SystemConfigHistory>
{
    public void Configure(EntityTypeBuilder<SystemConfigHistory> b)
    {
        b.ToTable("system_config_history");
        b.HasKey(x => x.Id);
        b.Property(x => x.ConfigKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.OldValue).HasMaxLength(500);
        b.Property(x => x.NewValue).HasMaxLength(500).IsRequired();
        b.Property(x => x.Note).HasMaxLength(1000).IsRequired();   // D9: mandatory reason

        b.HasIndex(x => new { x.ConfigKey, x.ChangedAt });

        b.HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class PerformerConfiguration : IEntityTypeConfiguration<Performer>
{
    public void Configure(EntityTypeBuilder<Performer> b)
    {
        b.ToTable("performers");
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.AvatarUrl).HasMaxLength(500);
        b.Property(p => p.Bio).HasMaxLength(2000);
        b.Property(p => p.Type).HasConversion<string>().HasMaxLength(20);

        b.HasOne(p => p.CreatedByUser)
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

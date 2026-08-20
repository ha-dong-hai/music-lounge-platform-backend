using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class AiPosterGenerationConfiguration : IEntityTypeConfiguration<AiPosterGeneration>
{
    public void Configure(EntityTypeBuilder<AiPosterGeneration> b)
    {
        b.ToTable("ai_poster_generations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Prompt).HasMaxLength(2000).IsRequired();
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.ErrorMessage).HasMaxLength(1000);

        b.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        b.HasIndex(x => new { x.ShowId, x.CreatedAt });

        b.HasOne(x => x.Show)
            .WithMany()
            .HasForeignKey(x => x.ShowId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

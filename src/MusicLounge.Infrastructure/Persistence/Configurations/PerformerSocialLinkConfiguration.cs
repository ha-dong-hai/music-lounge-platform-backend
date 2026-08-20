using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class PerformerSocialLinkConfiguration : IEntityTypeConfiguration<PerformerSocialLink>
{
    public void Configure(EntityTypeBuilder<PerformerSocialLink> b)
    {
        b.ToTable("performer_social_links");
        b.HasKey(x => x.Id);
        b.Property(x => x.Platform).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Url).HasMaxLength(500).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(255);

        // One link per platform per performer — a second Spotify link would just overwrite the
        // first from the UI's perspective anyway, and this keeps "does this performer already have
        // a Spotify link" a simple existence check.
        b.HasIndex(x => new { x.PerformerId, x.Platform }).IsUnique();

        b.HasOne(x => x.Performer)
            .WithMany()
            .HasForeignKey(x => x.PerformerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class UserCustomPreferenceConfiguration : IEntityTypeConfiguration<UserCustomPreference>
{
    public void Configure(EntityTypeBuilder<UserCustomPreference> b)
    {
        b.ToTable("user_custom_preferences");
        b.HasKey(x => x.Id);
        b.Property(x => x.Value).HasMaxLength(500).IsRequired();
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Weight).HasPrecision(4, 3).HasDefaultValue(0.5m);

        b.HasIndex(x => new { x.UserId, x.CriteriaId }).IsUnique();

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Criteria)
            .WithMany(c => c.UserPreferences)
            .HasForeignKey(x => x.CriteriaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

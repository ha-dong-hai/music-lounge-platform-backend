using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Title).HasMaxLength(NotificationLimits.TitleMaxLength).IsRequired();
        b.Property(x => x.Body).HasMaxLength(NotificationLimits.BodyMaxLength).IsRequired();
        b.Property(x => x.TitleEn).HasMaxLength(NotificationLimits.TitleMaxLength);
        b.Property(x => x.BodyEn).HasMaxLength(NotificationLimits.BodyMaxLength);
        b.Property(x => x.ReferenceType).HasMaxLength(50);
        b.Property(x => x.ReferenceId).HasMaxLength(100);
        b.Property(x => x.IsRead).HasDefaultValue(false);

        b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

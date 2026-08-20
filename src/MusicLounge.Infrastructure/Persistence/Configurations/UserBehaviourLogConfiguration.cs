using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class UserBehaviourLogConfiguration : IEntityTypeConfiguration<UserBehaviourLog>
{
    public void Configure(EntityTypeBuilder<UserBehaviourLog> b)
    {
        b.ToTable("user_behaviour_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Metadata).HasMaxLength(2000);
        b.HasIndex(x => new { x.UserId, x.LoungeShowId, x.Action });
        b.HasIndex(x => x.CreatedAt);

        b.HasOne(x => x.User)
            .WithMany(u => u.BehaviourLogs)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.LoungeShow)
            .WithMany(s => s.BehaviourLogs)
            .HasForeignKey(x => x.LoungeShowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

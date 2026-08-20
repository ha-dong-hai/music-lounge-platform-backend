using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

public sealed class LivestreamChatMessageConfiguration : IEntityTypeConfiguration<LivestreamChatMessage>
{
    public void Configure(EntityTypeBuilder<LivestreamChatMessage> builder)
    {
        builder.ToTable("livestream_chat_messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Message).IsRequired().HasMaxLength(500);

        builder.HasOne(m => m.Livestream)
            .WithMany(l => l.ChatMessages)
            .HasForeignKey(m => m.LivestreamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.LivestreamId, m.SentAt });
    }
}

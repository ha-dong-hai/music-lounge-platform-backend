using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LivestreamViewingSessionConfiguration : IEntityTypeConfiguration<LivestreamViewingSession>
{
    public void Configure(EntityTypeBuilder<LivestreamViewingSession> b)
    {
        b.ToTable("livestream_viewing_sessions");
        b.HasKey(s => s.Id);

        b.Property(s => s.SessionId).IsRequired().HasMaxLength(64);
        b.HasIndex(s => s.SessionId).IsUnique();
        // Đếm số phiên đang hoạt động của 1 vé (TicketId + LastHeartbeatAt >= cutoff) — truy vấn
        // nóng nhất trên bảng này, chạy mỗi lần GetLivestreamDetailQuery cấp HlsUrl.
        b.HasIndex(s => new { s.TicketId, s.LastHeartbeatAt });

        b.HasOne(s => s.Ticket)
            .WithMany()
            .HasForeignKey(s => s.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(s => s.Livestream)
            .WithMany()
            .HasForeignKey(s => s.LivestreamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

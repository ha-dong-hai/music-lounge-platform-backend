using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LivestreamTicketDetailConfiguration
    : IEntityTypeConfiguration<LivestreamTicketDetail>
{
    public void Configure(EntityTypeBuilder<LivestreamTicketDetail> b)
    {
        b.ToTable("livestream_ticket_details");
        b.HasKey(d => d.TicketId);

        b.Property(d => d.AccessToken).HasMaxLength(500);
        b.HasIndex(d => d.AccessToken).IsUnique().HasFilter("[AccessToken] IS NOT NULL");
        b.HasIndex(d => d.LivestreamId);

        b.HasOne(d => d.Ticket)
            .WithOne(t => t.LivestreamDetail)
            .HasForeignKey<LivestreamTicketDetail>(d => d.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(d => d.Livestream)
            .WithMany()
            .HasForeignKey(d => d.LivestreamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

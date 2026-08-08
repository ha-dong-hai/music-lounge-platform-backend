using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class PhysicalTicketDetailConfiguration
    : IEntityTypeConfiguration<PhysicalTicketDetail>
{
    public void Configure(EntityTypeBuilder<PhysicalTicketDetail> b)
    {
        b.ToTable("physical_ticket_details");
        b.HasKey(d => d.TicketId);

        b.Property(d => d.SeatInfo).HasMaxLength(100);
        b.Property(d => d.SoldByStaffId);

        b.HasOne(d => d.Ticket)
            .WithOne(t => t.PhysicalDetail)
            .HasForeignKey<PhysicalTicketDetail>(d => d.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

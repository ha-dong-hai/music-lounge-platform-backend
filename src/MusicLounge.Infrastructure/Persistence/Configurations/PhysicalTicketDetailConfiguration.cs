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

        b.HasOne(d => d.Ticket)
            .WithOne(t => t.PhysicalDetail)
            .HasForeignKey<PhysicalTicketDetail>(d => d.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Audit fields ("who sold/checked in this ticket") — previously unconstrained int? columns,
        // unlike every other "who did this" field in the codebase (Complaint.Admin,
        // EventModeration.Admin, FnbOrder.Staff, LoungeStaff.AssignedByUser all have a real FK).
        // Restrict (NO ACTION), not SetNull: SQL Server refuses to create this schema with SetNull
        // here — physical_ticket_details already reaches users via TicketId→tickets(Cascade)→
        // BuyerId(SetNull), so a second AND third cascading path straight to users (SetNull on
        // both Staff FKs) hits SQL Server's "multiple cascade paths" restriction (error 1785) —
        // caught only by running an actual SQL Server migration, not the SQLite test harness's
        // EnsureCreatedAsync, which doesn't enforce this. Restrict is safe regardless: this
        // codebase's erasure design (RequestDataErasureCommandHandler) anonymizes the User row in
        // place and never issues a hard DELETE, so this FK is never actually exercised.
        b.HasOne(d => d.SoldByStaff)
            .WithMany()
            .HasForeignKey(d => d.SoldByStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(d => d.CheckedInByStaff)
            .WithMany()
            .HasForeignKey(d => d.CheckedInByStaffId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

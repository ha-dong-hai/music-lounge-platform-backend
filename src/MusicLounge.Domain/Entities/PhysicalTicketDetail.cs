namespace MusicLounge.Domain.Entities;

public sealed class PhysicalTicketDetail
{
    public Guid TicketId { get; set; }
    public string? SeatInfo { get; set; }
    public Guid? SoldByStaffId { get; set; }
    public DateTimeOffset? CheckedInAt { get; set; }
    public Guid? CheckedInByStaffId { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public User? SoldByStaff { get; set; }
    public User? CheckedInByStaff { get; set; }
}

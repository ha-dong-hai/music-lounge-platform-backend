namespace MusicLounge.Domain.Entities;

public sealed class PhysicalTicketDetail
{
    public Guid TicketId { get; set; }
    public string? SeatInfo { get; set; }
    public int? SoldByStaffId { get; set; }
    public DateTimeOffset? CheckedInAt { get; set; }
    public int? CheckedInByStaffId { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public User? SoldByStaff { get; set; }
    public User? CheckedInByStaff { get; set; }
}

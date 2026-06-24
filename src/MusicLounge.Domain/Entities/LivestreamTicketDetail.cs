using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class LivestreamTicketDetail : BaseEntity<int>
{
    public Guid TicketId { get; set; }
    public int LivestreamId { get; set; }
    // Secret token used to verify viewer has a valid ticket before granting stream access
    public string AccessToken { get; set; } = string.Empty;
    // Null = viewer has not joined yet
    public DateTime? FirstAccessedAt { get; set; }
    // Tracked to detect abandoned streams and measure engagement duration
    public DateTime? LastAccessedAt { get; set; }
}

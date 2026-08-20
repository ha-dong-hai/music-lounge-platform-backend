namespace MusicLounge.Domain.Entities;

public sealed class LivestreamTicketDetail
{
    public Guid TicketId { get; set; }
    public int LivestreamId { get; set; }               // FK→livestreams RESTRICT — query all tokens for a stream
    public string? AccessToken { get; set; }            // D10: per-user secret — verify quyền xem, có thể revoke
    public DateTimeOffset? FirstAccessedAt { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public Livestream Livestream { get; set; } = null!;
}

namespace MusicLounge.Domain.Entities;

public sealed class LivestreamTicketDetail
{
    public Guid TicketId { get; set; }
    public int LivestreamId { get; set; }               // FK→livestreams RESTRICT — query all tokens for a stream
    // D10 du dinh day la bi mat rieng cua tung nguoi de kiem quyen xem va thu hoi duoc. THUC TE hien nay
    // (MLACP-356) khong cho nao doc token nay de kiem quyen: no chi duoc sinh ra (ProcessVnPayCallback,
    // StartLivestream, AcceptTicketTransfer) va tra ve cho client. Quyen xem that nam o
    // LivestreamRepository.HasViewerAccessAsync — nguoi dang nhap (JWT) so huu ve hang Livestream
    // Confirmed/Used cua dung buoi dien.
    public string? AccessToken { get; set; }
    public DateTimeOffset? FirstAccessedAt { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public Livestream Livestream { get; set; } = null!;
}

using MusicLounge.Application.Livestreams.DTOs;

namespace MusicLounge.Application.Common.Interfaces;

public interface ILivestreamHubService
{
    Task BroadcastChatMessageAsync(int livestreamId, ChatMessageDto message, CancellationToken ct = default);
    Task BroadcastReactionAsync(int livestreamId, string reactionType, CancellationToken ct = default);
    Task BroadcastDonationAlertAsync(int livestreamId, DonationAlertDto donation, CancellationToken ct = default);

    // MLACP-360: phòng trà gỡ lời nhắn của một donate — client xoá nó khỏi màn hình.
    Task BroadcastDonationMessageHiddenAsync(int livestreamId, int donationId, CancellationToken ct = default);

    /// <summary>MLACP-456: mot tin nhan chat vua bi go theo bao cao vi pham — nguoi dang xem phai thay no bien mat ngay.</summary>
    Task BroadcastChatMessageHiddenAsync(int livestreamId, int chatMessageId, CancellationToken ct = default);
    Task BroadcastViewerCountAsync(int livestreamId, int count, CancellationToken ct = default);
    Task BroadcastLivestreamTerminatedAsync(int livestreamId, string reason, CancellationToken ct = default);

    // MLACP-191: cho phia khan gia hien thong bao "dang ket noi lai" thay vi man hinh den khi
    // encoder mat ket noi dot ngot, va cap nhat lai khi da phat song tro lai / het gio cho.
    Task BroadcastLivestreamReconnectingAsync(int livestreamId, CancellationToken ct = default);
    Task BroadcastLivestreamReconnectedAsync(int livestreamId, CancellationToken ct = default);
    Task BroadcastLivestreamFailedAsync(int livestreamId, CancellationToken ct = default);
}

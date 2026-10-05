using MusicLounge.Application.Livestreams.DTOs;

namespace MusicLounge.Application.Common.Interfaces;

public interface ILivestreamHubService
{
    Task BroadcastChatMessageAsync(Guid livestreamId, ChatMessageDto message, CancellationToken ct = default);
    Task BroadcastReactionAsync(Guid livestreamId, string reactionType, CancellationToken ct = default);
    Task BroadcastDonationAlertAsync(Guid livestreamId, DonationAlertDto donation, CancellationToken ct = default);

    // MLACP-360: phòng trà gỡ lời nhắn của một donate — client xoá nó khỏi màn hình.
    Task BroadcastDonationMessageHiddenAsync(Guid livestreamId, Guid donationId, CancellationToken ct = default);

    /// <summary>MLACP-456: mot tin nhan chat vua bi go theo bao cao vi pham — nguoi dang xem phai thay no bien mat ngay.</summary>
    Task BroadcastChatMessageHiddenAsync(Guid livestreamId, Guid chatMessageId, CancellationToken ct = default);
    Task BroadcastViewerCountAsync(Guid livestreamId, int count, CancellationToken ct = default);
    Task BroadcastLivestreamTerminatedAsync(Guid livestreamId, string reason, CancellationToken ct = default);

    // MLACP-191: cho phia khan gia hien thong bao "dang ket noi lai" thay vi man hinh den khi
    // encoder mat ket noi dot ngot, va cap nhat lai khi da phat song tro lai / het gio cho.
    Task BroadcastLivestreamReconnectingAsync(Guid livestreamId, CancellationToken ct = default);
    Task BroadcastLivestreamReconnectedAsync(Guid livestreamId, CancellationToken ct = default);
    Task BroadcastLivestreamFailedAsync(Guid livestreamId, CancellationToken ct = default);

    /// <summary>MLACP-508: buổi phát đã KẾT THÚC bình thường (chủ phòng trà bấm Kết thúc, hoặc encoder ngừng hẳn) —
    /// người đang xem chuyển sang màn "đã kết thúc" thay vì trình phát đứng im tới khi tải lại trang. Không có xem lại.</summary>
    Task BroadcastLivestreamEndedAsync(Guid livestreamId, CancellationToken ct = default);

    /// <summary>MLACP-643: chủ phòng trà vừa bật/tắt khung chat — người đang xem đổi ô nhập ngay (khoá + câu báo) thay vì
    /// gõ xong mới biết bị chặn. Trước đây không có sự kiện nào: ô nhập vẫn mở, người gửi chỉ nhận câu lỗi chung.</summary>
    Task BroadcastChatEnabledChangedAsync(Guid livestreamId, bool enabled, CancellationToken ct = default);
}

using MusicLounge.Application.Livestreams.DTOs;

namespace MusicLounge.Application.Common.Interfaces;

public interface ILivestreamHubService
{
    Task BroadcastChatMessageAsync(int livestreamId, ChatMessageDto message, CancellationToken ct = default);
    Task BroadcastReactionAsync(int livestreamId, string reactionType, CancellationToken ct = default);
    Task BroadcastDonationAlertAsync(int livestreamId, DonationAlertDto donation, CancellationToken ct = default);
    Task BroadcastViewerCountAsync(int livestreamId, int count, CancellationToken ct = default);
    Task BroadcastLivestreamTerminatedAsync(int livestreamId, string reason, CancellationToken ct = default);
}

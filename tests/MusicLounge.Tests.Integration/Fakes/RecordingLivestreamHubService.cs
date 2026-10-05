using System.Collections.Concurrent;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Livestreams.DTOs;

namespace MusicLounge.Tests.Integration.Fakes;

/// <summary>
/// MLACP-360 — ghi lại mọi sự kiện gửi xuống livestream để test khẳng định được cái người xem thật sự
/// nhận. Trong test không có client SignalR nào kết nối, nên bản thật vốn cũng không làm gì quan sát
/// được; thay bằng bản ghi lại không đổi hành vi của test nào khác.
/// </summary>
public sealed class RecordingLivestreamHubService : ILivestreamHubService
{
    public sealed record Sent(Guid LivestreamId, string Event, object? Payload);

    private readonly ConcurrentQueue<Sent> _sent = new();

    public IReadOnlyList<Sent> For(Guid livestreamId) => _sent.Where(s => s.LivestreamId == livestreamId).ToList();

    private Task Record(Guid livestreamId, string evt, object? payload)
    {
        _sent.Enqueue(new Sent(livestreamId, evt, payload));
        return Task.CompletedTask;
    }

    public Task BroadcastChatMessageAsync(Guid livestreamId, ChatMessageDto message, CancellationToken ct = default)
        => Record(livestreamId, "ReceiveMessage", message);

    public Task BroadcastReactionAsync(Guid livestreamId, string reactionType, CancellationToken ct = default)
        => Record(livestreamId, "ReceiveReaction", reactionType);

    public Task BroadcastDonationAlertAsync(Guid livestreamId, DonationAlertDto donation, CancellationToken ct = default)
        => Record(livestreamId, "DonationAlert", donation);

    public Task BroadcastDonationMessageHiddenAsync(Guid livestreamId, Guid donationId, CancellationToken ct = default)
        => Record(livestreamId, "DonationMessageHidden", donationId);

    public Task BroadcastChatMessageHiddenAsync(Guid livestreamId, Guid chatMessageId, CancellationToken ct = default)
        => Record(livestreamId, "ChatMessageHidden", chatMessageId);

    public Task BroadcastViewerCountAsync(Guid livestreamId, int count, CancellationToken ct = default)
        => Record(livestreamId, "ViewerCountUpdated", count);

    public Task BroadcastLivestreamTerminatedAsync(Guid livestreamId, string reason, CancellationToken ct = default)
        => Record(livestreamId, "LivestreamTerminated", reason);

    public Task BroadcastLivestreamReconnectingAsync(Guid livestreamId, CancellationToken ct = default)
        => Record(livestreamId, "LivestreamReconnecting", null);

    public Task BroadcastLivestreamReconnectedAsync(Guid livestreamId, CancellationToken ct = default)
        => Record(livestreamId, "LivestreamReconnected", null);

    public Task BroadcastLivestreamFailedAsync(Guid livestreamId, CancellationToken ct = default)
        => Record(livestreamId, "LivestreamFailed", null);

    public Task BroadcastLivestreamEndedAsync(Guid livestreamId, CancellationToken ct = default)
        => Record(livestreamId, "LivestreamEnded", null);

    public Task BroadcastChatEnabledChangedAsync(Guid livestreamId, bool enabled, CancellationToken ct = default)
        => Record(livestreamId, "ChatEnabledChanged", enabled);
}

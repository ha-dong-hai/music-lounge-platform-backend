using Microsoft.AspNetCore.SignalR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Infrastructure.Hubs;

namespace MusicLounge.Infrastructure.Services;

public sealed class LivestreamHubService : ILivestreamHubService
{
    private readonly IHubContext<LivestreamHub> _hubContext;

    public LivestreamHubService(IHubContext<LivestreamHub> hubContext) => _hubContext = hubContext;

    public Task BroadcastChatMessageAsync(Guid livestreamId, ChatMessageDto message, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("ReceiveMessage", message, ct);

    public Task BroadcastReactionAsync(Guid livestreamId, string reactionType, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("ReceiveReaction", new { reactionType }, ct);

    public Task BroadcastDonationAlertAsync(Guid livestreamId, DonationAlertDto donation, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("DonationAlert", donation, ct);

    public Task BroadcastDonationMessageHiddenAsync(Guid livestreamId, Guid donationId, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("DonationMessageHidden", new { donationId }, ct);

    public Task BroadcastChatMessageHiddenAsync(Guid livestreamId, Guid chatMessageId, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("ChatMessageHidden", new { chatMessageId }, ct);

    public Task BroadcastViewerCountAsync(Guid livestreamId, int count, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("ViewerCountUpdated", new { count }, ct);

    public Task BroadcastLivestreamTerminatedAsync(Guid livestreamId, string reason, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("LivestreamTerminated", new { reason }, ct);

    public Task BroadcastLivestreamReconnectingAsync(Guid livestreamId, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("LivestreamReconnecting", new { }, ct);

    public Task BroadcastLivestreamReconnectedAsync(Guid livestreamId, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("LivestreamReconnected", new { }, ct);

    public Task BroadcastLivestreamFailedAsync(Guid livestreamId, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("LivestreamFailed", new { }, ct);

    public Task BroadcastLivestreamEndedAsync(Guid livestreamId, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("LivestreamEnded", new { }, ct);

    public Task BroadcastChatEnabledChangedAsync(Guid livestreamId, bool enabled, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("ChatEnabledChanged", new { enabled }, ct);
}

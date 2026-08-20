using Microsoft.AspNetCore.SignalR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Infrastructure.Hubs;

namespace MusicLounge.Infrastructure.Services;

public sealed class LivestreamHubService : ILivestreamHubService
{
    private readonly IHubContext<LivestreamHub> _hubContext;

    public LivestreamHubService(IHubContext<LivestreamHub> hubContext) => _hubContext = hubContext;

    public Task BroadcastChatMessageAsync(int livestreamId, ChatMessageDto message, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("ReceiveMessage", message, ct);

    public Task BroadcastReactionAsync(int livestreamId, string reactionType, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("ReceiveReaction", new { reactionType }, ct);

    public Task BroadcastDonationAlertAsync(int livestreamId, DonationAlertDto donation, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("DonationAlert", donation, ct);

    public Task BroadcastViewerCountAsync(int livestreamId, int count, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("ViewerCountUpdated", new { count }, ct);

    public Task BroadcastLivestreamTerminatedAsync(int livestreamId, string reason, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(LivestreamHub.GroupName(livestreamId))
            .SendAsync("LivestreamTerminated", new { reason }, ct);
}

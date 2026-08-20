using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common;
using MusicLounge.Application.Livestreams.Commands.SendChatMessage;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Hubs;

[Authorize]
public sealed class LivestreamHub : Hub
{
    private const string JoinedMarkerKey = "joined";

    private readonly IMediator _mediator;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationDbContext _ctx;
    private readonly ILivestreamHubService _hubService;

    public LivestreamHub(
        IMediator mediator,
        ILivestreamRepository livestreamRepo,
        ICurrentUserService currentUser,
        ApplicationDbContext ctx,
        ILivestreamHubService hubService)
    {
        _mediator = mediator;
        _livestreamRepo = livestreamRepo;
        _currentUser = currentUser;
        _ctx = ctx;
        _hubService = hubService;
    }

    public override async Task OnConnectedAsync()
    {
        var livestreamId = GetLivestreamId();
        if (livestreamId is null) { Context.Abort(); return; }

        // Was previously any Staff/Admin account regardless of venue — let Staff of venue A join
        // the group and receive venue B's paid livestream for free. Mirrors the fix already applied
        // to GetLivestreamDetailQueryHandler/GetChatHistoryQueryHandler: Admin bypasses fully,
        // Staff/Owner must operate the SAME venue as this livestream, everyone else falls back to
        // the real ticket-based viewer check.
        var hasAccess = _currentUser.Role == Roles.Admin;
        if (!hasAccess)
        {
            var venue = await _ctx.Livestreams
                .Where(l => l.Id == livestreamId.Value)
                .Select(l => new { l.LoungeShow.LoungeId, OwnerId = l.LoungeShow.Lounge.OwnerId })
                .FirstOrDefaultAsync();
            hasAccess = (venue is not null && VenueOperatorAccess.CanOperate(_currentUser, venue.LoungeId, venue.OwnerId))
                || await _livestreamRepo.HasViewerAccessAsync(livestreamId.Value, _currentUser.UserId);
        }
        if (!hasAccess) { Context.Abort(); return; }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(livestreamId.Value));

        // Atomic increment — avoids race condition from read-modify-write
        await _ctx.Livestreams
            .Where(l => l.Id == livestreamId.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ViewerCount, l => l.ViewerCount + 1));

        // Marks that THIS connection actually incremented the count, so OnDisconnectedAsync only
        // decrements for connections that got past the access check above — a connection rejected
        // by Context.Abort() still fires OnDisconnectedAsync, and without this marker it would
        // decrement a count it never contributed to (every failed join attempt — retry, no ticket,
        // wrong-venue Staff — would silently drag the displayed viewer count down).
        Context.Items[JoinedMarkerKey] = true;

        var newCount = await _ctx.Livestreams
            .Where(l => l.Id == livestreamId.Value)
            .Select(l => l.ViewerCount)
            .FirstOrDefaultAsync();

        await _hubService.BroadcastViewerCountAsync(livestreamId.Value, newCount);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var livestreamId = GetLivestreamId();
        if (livestreamId is not null && Context.Items.ContainsKey(JoinedMarkerKey))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(livestreamId.Value));

            // Atomic decrement — floor at 0 to prevent negative counts
            await _ctx.Livestreams
                .Where(l => l.Id == livestreamId.Value && l.ViewerCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.ViewerCount, l => l.ViewerCount - 1));

            var newCount = await _ctx.Livestreams
                .Where(l => l.Id == livestreamId.Value)
                .Select(l => l.ViewerCount)
                .FirstOrDefaultAsync();

            await _hubService.BroadcastViewerCountAsync(livestreamId.Value, newCount);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string message)
    {
        var livestreamId = GetLivestreamId();
        if (livestreamId is null) return;

        await _mediator.Send(new SendChatMessageCommand(
            livestreamId.Value,
            _currentUser.UserId,
            message));
    }

    private static readonly HashSet<string> _allowedReactions = ["like", "heart", "fire", "wow"];

    public async Task SendReaction(string reactionType)
    {
        var livestreamId = GetLivestreamId();
        if (livestreamId is null) return;
        if (!_allowedReactions.Contains(reactionType)) return;

        await _hubService.BroadcastReactionAsync(livestreamId.Value, reactionType);
    }

    private int? GetLivestreamId()
    {
        var value = Context.GetHttpContext()?.Request.Query["livestreamId"].ToString();
        return int.TryParse(value, out var id) ? id : null;
    }

    public static string GroupName(int livestreamId) => $"livestream-{livestreamId}";
    public static string StaffGroupName(int livestreamId) => $"livestream-staff-{livestreamId}";
}

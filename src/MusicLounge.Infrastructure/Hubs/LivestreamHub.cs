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
        var isAdmin = _currentUser.Role == Roles.Admin;
        var isVenueOperator = false;
        var hasAccess = isAdmin;
        if (!hasAccess)
        {
            var venue = await _ctx.Livestreams
                .Where(l => l.Id == livestreamId.Value)
                .Select(l => new { l.LoungeShow.LoungeId, OwnerId = l.LoungeShow.Lounge.OwnerId, l.IsFree })
                .FirstOrDefaultAsync();
            // MLACP-117: livestream mien phi khong yeu cau ve — phai dong bo voi
            // GetLivestreamDetailQueryHandler, neu khong nguoi xem se thay duoc HlsUrl qua REST
            // nhung bi Context.Abort() ngay khi hub co gang join group cua chinh stream do.
            isVenueOperator = venue is not null && VenueOperatorAccess.CanOperate(_currentUser, venue.LoungeId, venue.OwnerId);
            hasAccess = isVenueOperator
                || (venue is not null && venue.IsFree)
                || await _livestreamRepo.HasViewerAccessAsync(livestreamId.Value, _currentUser.UserId);
        }
        if (!hasAccess) { Context.Abort(); return; }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(livestreamId.Value));

        // MLACP-646: Admin và người vận hành phòng trà vào để GIÁM SÁT — vẫn nhận chat/sự kiện như mọi người xem, nhưng
        // không tính vào số người xem (đo 05/10/2026: 5 khán giả mà đỉnh ghi 7, tổng lượt 8 — số liệu chủ phòng trà đọc ở
        // trang thống kê và khán giả thấy "N người đang xem"). Không đánh dấu JoinedMarker nên lúc rời cũng không trừ.
        if (!CountsAsAudience(isAdmin, isVenueOperator))
        {
            await base.OnConnectedAsync();
            return;
        }

        // Dem nguoi xem nam trong repository chu khong o day: PeakViewerCount va TotalViews truoc
        // MLACP-303 khong ai ghi — trang thong ke cua chu phong tra luon hien 0 nguoi xem cho moi
        // buoi da phat — va hub SignalR gan nhu khong kiem thu duoc, nen phan logic do phai o cho
        // co the viet test.
        var joinedCount = await _livestreamRepo.RecordViewerJoinedAsync(livestreamId.Value);

        // Marks that THIS connection actually incremented the count, so OnDisconnectedAsync only
        // decrements for connections that got past the access check above — a connection rejected
        // by Context.Abort() still fires OnDisconnectedAsync, and without this marker it would
        // decrement a count it never contributed to (every failed join attempt — retry, no ticket,
        // wrong-venue Staff — would silently drag the displayed viewer count down).
        Context.Items[JoinedMarkerKey] = true;

        await _hubService.BroadcastViewerCountAsync(livestreamId.Value, joinedCount);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var livestreamId = GetLivestreamId();
        if (livestreamId is not null && Context.Items.ContainsKey(JoinedMarkerKey))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(livestreamId.Value));

            var newCount = await _livestreamRepo.RecordViewerLeftAsync(livestreamId.Value);

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

    private Guid? GetLivestreamId()
    {
        var value = Context.GetHttpContext()?.Request.Query["livestreamId"].ToString();
        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>MLACP-646: chỉ khán giả mới được đếm — Admin và chủ/nhân viên của chính phòng trà vào để giám sát.</summary>
    public static bool CountsAsAudience(bool isAdmin, bool isVenueOperator) => !isAdmin && !isVenueOperator;

    public static string GroupName(Guid livestreamId) => $"livestream-{livestreamId}";
    public static string StaffGroupName(Guid livestreamId) => $"livestream-staff-{livestreamId}";
}

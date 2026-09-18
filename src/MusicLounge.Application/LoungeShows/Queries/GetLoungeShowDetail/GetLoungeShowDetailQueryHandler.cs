using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.LoungeShows.Queries.GetLoungeShowDetail;

internal sealed class GetLoungeShowDetailQueryHandler
    : IRequestHandler<GetLoungeShowDetailQuery, LoungeShowDetailDto>
{
    private readonly ILoungeShowRepository _showRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IBackgroundJobService _jobs;
    private readonly IUnitOfWork _uow;
    private readonly ISystemConfigService _config;

    public GetLoungeShowDetailQueryHandler(
        ILoungeShowRepository showRepo,
        ICurrentUserService currentUser,
        IBackgroundJobService jobs,
        IUnitOfWork uow,
        ISystemConfigService config)
    {
        _showRepo = showRepo;
        _currentUser = currentUser;
        _jobs = jobs;
        _uow = uow;
        _config = config;
    }

    public async Task<LoungeShowDetailDto> Handle(
        GetLoungeShowDetailQuery request, CancellationToken ct)
    {
        var show = await _showRepo.GetByIdWithDetailsAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.LoungeShow), request.ShowId);

        // Was previously any Staff/Admin account regardless of venue — Staff of venue A could see
        // venue B's Draft show (unpublished price/lineup/description). Admin still bypasses fully;
        // Staff/Owner must actually operate THIS show's venue, matching the pattern already used
        // for livestream credentials/detail.
        // MLACP-60 DONE WHEN: Pending (da nop duyet, cho Admin, chua cong khai) cung phai 404 cho
        // nguoi ngoai — truoc day chi chan Draft, Pending bi lot ra cong khai y het gap da fix o
        // SearchAsync (MLACP-58).
        var canOperate = VenueOperatorAccess.CanOperate(_currentUser, show.LoungeId, show.Lounge.OwnerId);
        if (show.Status is LoungeShowStatus.Draft or LoungeShowStatus.Pending && !canOperate)
            throw new NotFoundException(nameof(Domain.Entities.LoungeShow), request.ShowId);

        var wishlisted = _currentUser.IsAuthenticated
            ? await _showRepo.GetWishlistedShowIdsAsync(_currentUser.UserId, ct)
            : (IReadOnlySet<int>)new HashSet<int>();

        if (_currentUser.IsAuthenticated)
        {
            // A user who already wishlisted this show coming back to view it again is a stronger
            // signal than a first-time view — logged as its own BehaviourAction rather than a plain
            // ViewEvent so RecomputeUserEventScoresJob can weight it accordingly.
            var action = wishlisted.Contains(request.ShowId)
                ? BehaviourAction.ViewAfterWishlist
                : BehaviourAction.ViewEvent;
            _jobs.EnqueueLogUserBehaviour(_currentUser.UserId, request.ShowId, action);
        }

        bool? userHasTicket = null;
        bool? userHasRated = null;
        if (_currentUser.IsAuthenticated)
        {
            userHasTicket = await _uow.Repository<Ticket, Guid>()
                .AnyAsync(t => t.ShowId == request.ShowId
                    && t.BuyerId == _currentUser.UserId
                    && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used), ct);
            userHasRated = await _uow.Repository<LoungeShowRating, int>()
                .AnyAsync(r => r.LoungeShowId == request.ShowId
                    && r.UserId == _currentUser.UserId, ct);
        }

        var priceIds = show.TicketTiers
            .SelectMany(t => t.Prices)
            .Select(p => p.Id)
            .ToList();
        var soldAndHeld = await _showRepo.GetSoldAndHeldCountsByPriceAsync(priceIds, ct);

        // MusicLounge entity khong co nav collection toi LoungeGalleryImage — truy van rieng thay
        // vi Include tu WithDetails().
        var galleryImages = await _uow.Repository<LoungeGalleryImage, int>()
            .FindAsync(g => g.LoungeId == show.LoungeId, ct);
        var galleryDtos = galleryImages
            .OrderBy(g => g.OrderIndex)
            .Select(g => new LoungeGalleryImageDto(g.Id, g.ImageUrl, g.Caption, g.OrderIndex))
            .ToList();

        var lastEntryMinutes = await _config.GetIntAsync(
            ConfigKeys.TicketLastEntryMinutes, TicketSaleWindow.DefaultLastEntryMinutes, ct);

        var dto = show.ToDetailDto(
            wishlisted, userHasTicket, userHasRated, soldAndHeld, galleryDtos, lastEntryMinutes);

        // MLACP-450: cung mot phep kiem voi cho chan ban nhap o tren — ai thay duoc ban nhap thi thay duoc ly do bi tu
        // choi va ma VCPMC; khan gia thi khong.
        return canOperate ? dto with { OperatorInfo = await OperatorInfoAsync(show, ct) } : dto;
    }

    private async Task<OperatorShowInfoDto> OperatorInfoAsync(Domain.Entities.LoungeShow show, CancellationToken ct)
    {
        // Moi lan gui duyet la mot dong moi (bi tu choi -> sua -> gui lai), nen dong moi nhat la trang thai hien tai.
        var latest = (await _uow.Repository<EventModeration, int>()
                .FindAsync(m => m.TargetType == ModerationTargetType.Show && m.TargetId == show.Id, ct))
            .OrderByDescending(m => m.Id)
            .FirstOrDefault();

        var moderation = latest is null
            ? null
            : new ShowModerationDto(
                latest.AdminDecision,
                latest.ReviewNote,
                // AuditableEntity.CreatedAt la DateTime danh dau bang DateTime.UtcNow (ApplicationDbContext).
                new DateTimeOffset(latest.CreatedAt, TimeSpan.Zero),
                latest.ReviewedAt,
                latest.SlaDeadline);

        return new OperatorShowInfoDto(
            moderation,
            !string.IsNullOrWhiteSpace(show.VcpmcRoyaltyReference),
            show.VcpmcRoyaltyReference);
    }
}

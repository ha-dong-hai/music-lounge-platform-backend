using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Utils;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.PublishLoungeShow;

internal sealed class PublishLoungeShowCommandHandler : IRequestHandler<PublishLoungeShowCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly IEventModerationRepository _moderationRepo;
    private readonly ISystemConfigService _config;
    private readonly IAsyncKeyedLock _lock;

    public PublishLoungeShowCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ILivestreamRepository livestreamRepo,
        IEventModerationRepository moderationRepo,
        ISystemConfigService config,
        IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _livestreamRepo = livestreamRepo;
        _moderationRepo = moderationRepo;
        _config = config;
        _lock = @lock;
    }

    public async Task<Unit> Handle(PublishLoungeShowCommand request, CancellationToken ct)
    {
        // Without this, two concurrent Publish calls on the same Draft show both pass the
        // "existingModeration is null" check before either commits, and both insert an
        // EventModeration row — EventModerationConfiguration has no unique index on
        // (TargetType, TargetId), so nothing else stops the duplicate.
        await using var _ = await _lock.AcquireAsync($"moderation:show:{request.ShowId}", ct);

        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền nộp duyệt event này.");

        // §6.8 — a suspended/locked venue cannot bring new events live; existing published shows
        // are untouched (khán giả đã mua vé không bị ảnh hưởng bởi vi phạm của venue).
        if (lounge.Status is LoungeStatus.Suspended or LoungeStatus.Locked)
            throw new DomainException(
                $"Phòng trà hiện đang ở trạng thái '{lounge.Status}' do vi phạm — không thể nộp duyệt event mới.");

        if (show.Status != LoungeShowStatus.Draft)
            throw new DomainException("Chỉ có thể nộp duyệt event đang ở trạng thái Draft.");

        var tiers = await _uow.Repository<TicketTier, int>()
            .FindAsync(t => t.LoungeShowId == show.Id, ct);
        if (tiers.Count == 0)
            throw new DomainException("Event phải có ít nhất 1 hạng vé trước khi nộp duyệt.");

        // D18 (NĐ 144/2020/NĐ-CP Điều 10): show bán vé phải có văn bản chấp thuận biểu diễn +
        // nộp duyệt trước tối thiểu 7 ngày làm việc so với ScheduledStart. Mọi show trên platform
        // đều bán vé (bắt buộc >=1 tier ở trên) nên áp dụng đồng nhất, không cần nhánh rẽ.
        if (string.IsNullOrWhiteSpace(show.LegalApprovalReference))
            throw new DomainException(
                "Event bán vé cần khai báo văn bản chấp thuận tổ chức biểu diễn (NĐ 144/2020 Điều 10) " +
                "trước khi nộp duyệt.");

        var minLeadDays = await _config.GetIntAsync(ConfigKeys.PublishMinBusinessDaysLeadTime, 7, ct);
        var businessDaysUntilShow = BusinessDayCalculator.CountBusinessDaysBetween(
            DateTimeOffset.UtcNow, show.ScheduledStart);
        if (businessDaysUntilShow < minLeadDays)
            throw new DomainException(
                $"Theo NĐ 144/2020 Điều 10, event bán vé phải nộp duyệt trước tối thiểu {minLeadDays} ngày làm việc " +
                $"so với ngày diễn. Hiện chỉ còn {businessDaysUntilShow} ngày làm việc.");

        // D15: online event or any livestream-access tier requires a Livestream record before publish
        var needsLivestream = show.Format == LoungeShowFormat.Online
            || tiers.Any(t => t.AccessType == AccessType.Livestream);

        if (needsLivestream)
        {
            var livestream = await _livestreamRepo.GetByShowIdAsync(show.Id, ct);
            if (livestream is null)
                throw new DomainException(
                    "Event online hoặc có vé livestream phải được thiết lập Livestream trước khi nộp duyệt.");
        }

        show.Status = LoungeShowStatus.Pending;
        showRepo.Update(show);

        var existingModeration = await _moderationRepo.GetByTargetAsync(
            ModerationTargetType.Show, show.Id, ct);

        // NĐ 147/2024: 24h SLA to review — read from system_config (§6.7: never hardcode),
        // recomputed from "now" so a resubmission after rejection gets a fresh SLA window too.
        var slaHours = await _config.GetIntAsync(ConfigKeys.ModerationSlaHours, 24, ct);
        var now = DateTimeOffset.UtcNow;

        if (existingModeration is null)
        {
            _uow.Repository<EventModeration, int>().Add(new EventModeration
            {
                TargetType = ModerationTargetType.Show,
                TargetId = show.Id,
                CreatedAt = now,
                SlaDeadline = now.AddHours(slaHours)
            });
        }
        else
        {
            // Resubmission after a previous rejection — reopen for review
            existingModeration.AdminDecision = null;
            existingModeration.AdminId = null;
            existingModeration.ReviewNote = null;
            existingModeration.ReviewedAt = null;
            existingModeration.CreatedAt = now;
            existingModeration.SlaDeadline = now.AddHours(slaHours);
            _moderationRepo.Update(existingModeration);
        }

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

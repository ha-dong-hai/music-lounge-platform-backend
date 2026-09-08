using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Constants;
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
    private readonly IBackgroundJobService _backgroundJobs;

    public PublishLoungeShowCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ILivestreamRepository livestreamRepo,
        IEventModerationRepository moderationRepo,
        ISystemConfigService config,
        IAsyncKeyedLock @lock,
        IBackgroundJobService backgroundJobs)
    {
        _uow = uow;
        _currentUser = currentUser;
        _livestreamRepo = livestreamRepo;
        _moderationRepo = moderationRepo;
        _config = config;
        _lock = @lock;
        _backgroundJobs = backgroundJobs;
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

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
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

        // MLACP-46: DONE WHEN doi ca 2 dieu kien (hang ve + nghe si) - ban local master chi check
        // hang ve, thieu check danh sach bieu dien.
        var performerCount = await _uow.Repository<Performance, int>()
            .CountAsync(p => p.LoungeShowId == show.Id, ct);
        if (performerCount == 0)
            throw new DomainException("Event phải có ít nhất 1 nghệ sĩ trong danh sách biểu diễn trước khi nộp duyệt.");

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

        // MLACP-288. Checked here as well as in the validators because time moves between writing
        // a draft and submitting it: a deadline of "72 giờ trước giờ diễn" set three weeks out is
        // perfectly reachable then and already expired by the time the owner presses submit, with
        // nobody having edited anything. Publishing in that state would put a show on sale whose
        // page advertises a refund window that every single buyer has already missed.
        if (!TicketRefundPolicy.IsDeadlineStillReachable(show, DateTimeOffset.UtcNow))
            throw new DomainException(
                $"Hạn hủy vé ({show.CancellationDeadlineHours} giờ trước giờ diễn) đã trôi qua so " +
                $"với lịch diễn hiện tại, nên không người mua nào có thể dùng quyền hủy vé được " +
                $"công bố. Hãy rút ngắn hạn hủy, bỏ hạn hủy, hoặc dời lịch diễn trước khi nộp duyệt.");

        // A payout account is a precondition for SELLING, not just for getting paid. Every show on
        // this platform sells tickets (>=1 tier is required above), and ScheduleSettlementHandler
        // fails closed when the venue has no default BankAccount. That handler runs as a MediatR
        // notification inside ProcessVnPayCallback's transaction, so its throw rolled back the
        // buyer's confirmation *after* VNPay had already taken their money: payment stuck Pending,
        // VNPay's retries hitting the same exception, and CancelAbandonedPaymentsJob voiding the
        // tickets 30 minutes later. Money taken, no ticket, no refund. Checking here is what keeps
        // that path off the table in the first place; ScheduleSettlementHandler's own null-account
        // branch is the backstop for a venue that removes its account after publishing.
        var defaultPayoutAccounts = await _uow.Repository<BankAccount, int>().FindAsync(
            a => a.OwnerType == BankAccountOwnerType.Lounge && a.OwnerId == lounge.Id && a.IsDefault, ct);
        if (defaultPayoutAccounts.Count == 0)
            throw new DomainException(
                "Venue cần đăng ký tài khoản ngân hàng mặc định trước khi nộp duyệt event bán vé — " +
                "đây là tài khoản nhận tiền bán vé sau khi show diễn ra.");

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

        EventModeration moderation;
        if (existingModeration is null)
        {
            moderation = new EventModeration
            {
                TargetType = ModerationTargetType.Show,
                TargetId = show.Id,
                SlaDeadline = now.AddHours(slaHours)
            };
            _uow.Repository<EventModeration, int>().Add(moderation);
        }
        else
        {
            // Resubmission after a previous rejection — reopen for review
            moderation = existingModeration;
            moderation.AdminDecision = null;
            moderation.AdminId = null;
            moderation.ReviewNote = null;
            moderation.ReviewedAt = null;
            // AuditableEntity.CreatedAt is hook-managed on Added, but NOT touched on Modified — safe
            // to keep reusing it here as "review window start" for a resubmission (deliberate reuse,
            // predates the AuditableEntity conversion). .UtcDateTime matches the hook's own DateTime/
            // Kind=Utc shape for this column.
            moderation.CreatedAt = now.UtcDateTime;
            moderation.SlaDeadline = now.AddHours(slaHours);
            // Stale from the previous submission's scoring — re-score the (possibly edited)
            // content fresh rather than leave the old verdict sitting on a reopened review.
            moderation.AiScore = null;
            moderation.RiskLevel = null;
            moderation.FlagReason = null;
            moderation.AiRecommendation = null;
            _moderationRepo.Update(moderation);
        }

        await _uow.SaveChangesAsync(ct);

        _backgroundJobs.EnqueueModerationAiScoring(moderation.Id);

        return Unit.Value;
    }
}

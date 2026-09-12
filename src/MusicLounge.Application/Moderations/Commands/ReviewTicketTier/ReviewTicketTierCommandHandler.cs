using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Moderations.Commands.ReviewTicketTier;

/// <summary>
/// MLACP-388. Duyệt hạng vé livestream được thêm SAU khi buổi diễn đã đăng (thường là sau khi chuyển sang online —
/// MLACP-383). Buổi diễn đã qua kiểm duyệt với bộ hạng vé cũ; một hạng vé và mức giá mới là nội dung chưa ai duyệt,
/// nên giá của nó chỉ được bán sau khi Admin đồng ý — cùng quy trình với duyệt buổi diễn và livestream.
/// </summary>
internal sealed class ReviewTicketTierCommandHandler : IRequestHandler<ReviewTicketTierCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IEventModerationRepository _moderationRepo;
    private readonly INotificationService _notifications;
    private readonly IAsyncKeyedLock _lock;

    public ReviewTicketTierCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IEventModerationRepository moderationRepo,
        INotificationService notifications,
        IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _moderationRepo = moderationRepo;
        _notifications = notifications;
        _lock = @lock;
    }

    public async Task<Unit> Handle(ReviewTicketTierCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<ModerationDecision>(request.Decision, true, out var decision)
            || decision == ModerationDecision.Terminated)
            throw new DomainException("Quyết định không hợp lệ. Dùng 'Approved' hoặc 'Rejected'.");

        // Cung ly do voi ReviewShowCommandHandler: hai Admin duyet cung luc thi mot quyet dinh ghi de mat quyet dinh kia.
        await using var _ = await _lock.AcquireAsync($"moderation:tier:{request.TierId}", ct);

        var tier = await _uow.Repository<TicketTier, int>().GetByIdAsync(request.TierId, ct)
            ?? throw new NotFoundException(nameof(TicketTier), request.TierId);

        var moderation = await _moderationRepo.GetByTargetAsync(ModerationTargetType.TicketTier, request.TierId, ct)
            ?? throw new NotFoundException("EventModeration for TicketTier", request.TierId);

        if (moderation.AdminDecision is not null)
            throw new ConflictException("Hạng vé này đã được duyệt trước đó.");

        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(tier.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), tier.LoungeShowId);

        // Buoi dien da ket thuc hoac bi huy thi mo ban la ban mot thu khong con.
        if (show.Status is not (LoungeShowStatus.Published or LoungeShowStatus.Ongoing))
            throw new ConflictException("Buổi diễn đã kết thúc hoặc đã bị huỷ — không còn gì để mở bán.");

        var now = DateTimeOffset.UtcNow;
        moderation.AdminDecision = decision;
        moderation.AdminId = _currentUser.UserId;
        moderation.ReviewNote = request.ReviewNote;
        moderation.ReviewedAt = now;
        _moderationRepo.Update(moderation);

        if (decision == ModerationDecision.Approved)
        {
            var priceRepo = _uow.Repository<TicketPrice, int>();
            var prices = await priceRepo.FindAsync(p => p.TierId == tier.Id, ct);
            foreach (var price in prices)
            {
                price.IsActive = true;
                priceRepo.Update(price);
            }
        }

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct);
        if (lounge is not null)
            await _notifications.NotifyAsync(
                lounge.OwnerId,
                NotificationType.ModerationResult,
                decision == ModerationDecision.Approved ? "Hạng vé livestream đã được duyệt" : "Hạng vé livestream bị từ chối",
                decision == ModerationDecision.Approved
                    ? $"Hạng vé \"{tier.Name}\" của \"{show.Name}\" đã được duyệt và mở bán."
                    : $"Hạng vé \"{tier.Name}\" của \"{show.Name}\" bị từ chối. Lý do: {request.ReviewNote}. " +
                      "Hạng vé này không được mở bán.",
                referenceType: "show",
                referenceId: show.Id.ToString(),
                ct: ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

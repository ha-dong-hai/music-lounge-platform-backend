using FluentValidation;
using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows;
using MusicLounge.Application.VenuePenalties;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Domain.ValueObjects;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.ShowCancellationReviews;

// MLACP-676 — Admin xét lý do chủ phòng trà huỷ buổi hòa nhạc đã mở bán (xem ShowCancellationReview). Buổi diễn đã huỷ và
// khán giả đã được hoàn từ lúc huỷ; ở đây chỉ quyết phòng trà có bị phạt hay không.

public sealed record ShowCancellationReviewDto(
    Guid Id,
    Guid ShowId,
    string ShowName,
    DateTimeOffset ShowStart,
    Guid LoungeId,
    string LoungeName,
    string Reason,
    string ReasonLabel,
    string Detail,
    string? EvidenceUrl,
    int TicketsRefunded,
    decimal AmountRefunded,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset SlaDeadline,
    string? DecisionNote,
    DateTimeOffset? ReviewedAt,
    Guid? PenaltyId,
    // Số lần phòng trà này đã huỷ buổi đã mở bán trước lần này — tái phạm là căn cứ chính để phạt.
    int EarlierCancellations);

/// <param name="Status">Pending (mặc định) | Excused | Penalized | All.</param>
public sealed record GetShowCancellationReviewsQuery(string? Status = "Pending", int Page = 1, int PageSize = 20)
    : IRequest<PaginatedResult<ShowCancellationReviewDto>>;

internal sealed class GetShowCancellationReviewsQueryHandler
    : IRequestHandler<GetShowCancellationReviewsQuery, PaginatedResult<ShowCancellationReviewDto>>
{
    private readonly IUnitOfWork _uow;

    public GetShowCancellationReviewsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<PaginatedResult<ShowCancellationReviewDto>> Handle(
        GetShowCancellationReviewsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var all = await _uow.Repository<ShowCancellationReview, Guid>().GetAllAsync(ct);

        IEnumerable<ShowCancellationReview> loc = all;
        if (Enum.TryParse<ShowCancellationReviewStatus>(request.Status, true, out var st))
            loc = loc.Where(r => r.Status == st);

        // Chờ xét: hạn gần nhất lên trước. Đã xét: mới nhất lên trước.
        var ds = (st == ShowCancellationReviewStatus.Pending
                ? loc.OrderBy(r => r.SlaDeadline)
                : loc.OrderByDescending(r => r.CreatedAt))
            .ToList();
        var trang = ds.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var showIds = trang.Select(r => r.ShowId).Distinct().ToList();
        var shows = showIds.Count == 0 ? new Dictionary<Guid, LoungeShow>()
            : (await _uow.Repository<LoungeShow, Guid>().FindAsync(s => showIds.Contains(s.Id), ct)).ToDictionary(s => s.Id);
        var loungeIds = trang.Select(r => r.LoungeId).Distinct().ToList();
        var lounges = loungeIds.Count == 0 ? new Dictionary<Guid, MusicLoungeEntity>()
            : (await _uow.Repository<MusicLoungeEntity, Guid>().FindAsync(l => loungeIds.Contains(l.Id), ct)).ToDictionary(l => l.Id);

        var items = trang.Select(r =>
        {
            var show = shows.GetValueOrDefault(r.ShowId);
            return new ShowCancellationReviewDto(
                r.Id, r.ShowId, show?.Name ?? "", show?.ScheduledStart ?? default, r.LoungeId,
                lounges.GetValueOrDefault(r.LoungeId)?.Name ?? "", r.Reason.ToString(),
                ShowCancellationReasons.Label(r.Reason).Vi, r.Detail, r.EvidenceUrl, r.TicketsRefunded, r.AmountRefunded,
                r.Status.ToString(), r.CreatedAt, r.SlaDeadline, r.DecisionNote, r.ReviewedAt, r.PenaltyId,
                all.Count(o => o.LoungeId == r.LoungeId && o.CreatedAt < r.CreatedAt));
        }).ToList();

        return new PaginatedResult<ShowCancellationReviewDto>(items, page, pageSize, ds.Count);
    }
}

/// <param name="Decision">Excuse (miễn phạt) | Penalize (phạt).</param>
/// <param name="PenaltyType">Khi phạt: Warning (mặc định) | Suspension | Ban.</param>
/// <param name="SuspensionDays">Bắt buộc khi PenaltyType = Suspension.</param>
public sealed record DecideShowCancellationReviewCommand(
    Guid ReviewId, string Decision, string Note, string? PenaltyType = null, int? SuspensionDays = null) : ICommand;

public sealed class DecideShowCancellationReviewCommandValidator : AbstractValidator<DecideShowCancellationReviewCommand>
{
    public DecideShowCancellationReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId).NotEmpty();
        RuleFor(x => x.Decision).Must(d => d is "Excuse" or "Penalize")
            .WithMessage("Quyết định phải là Excuse (miễn phạt) hoặc Penalize (phạt).");
        RuleFor(x => x.Note).NotEmpty().WithMessage("Hãy ghi lý do của quyết định — chủ phòng trà sẽ đọc câu này.")
            .MaximumLength(1000);
        RuleFor(x => x.PenaltyType)
            .Must(p => p is null || Enum.TryParse<PenaltyType>(p, true, out _))
            .WithMessage("Mức phạt không hợp lệ.");
        RuleFor(x => x.SuspensionDays).NotNull().InclusiveBetween(1, 365)
            .When(x => x.Decision == "Penalize" && string.Equals(x.PenaltyType, "Suspension", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Tạm khoá cần số ngày (1–365).");
    }
}

internal sealed class DecideShowCancellationReviewCommandHandler : IRequestHandler<DecideShowCancellationReviewCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly ISystemConfigService _config;
    private readonly IAsyncKeyedLock _lock;

    public DecideShowCancellationReviewCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications,
        ISystemConfigService config, IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _config = config;
        _lock = @lock;
    }

    public async Task<Unit> Handle(DecideShowCancellationReviewCommand request, CancellationToken ct)
    {
        // Hai Admin bấm cùng lúc không được ra hai án phạt cho một lần huỷ.
        await using var _ = await _lock.AcquireAsync($"show-cancellation-review:{request.ReviewId}", ct);

        var repo = _uow.Repository<ShowCancellationReview, Guid>();
        var review = await repo.GetByIdAsync(request.ReviewId, ct)
            ?? throw new NotFoundException(nameof(ShowCancellationReview), request.ReviewId);
        if (review.Status != ShowCancellationReviewStatus.Pending)
            throw new ConflictException("Lý do huỷ này đã được xét.");

        var show = await _uow.Repository<LoungeShow, Guid>().GetByIdAsync(review.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), review.ShowId);
        var lounge = await _uow.Repository<MusicLoungeEntity, Guid>().GetByIdAsync(review.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), review.LoungeId);

        var now = DateTimeOffset.UtcNow;
        var note = request.Note.Trim();
        review.ReviewedBy = _currentUser.UserId;
        review.ReviewedAt = now;
        review.DecisionNote = note;

        if (request.Decision == "Penalize")
        {
            var mucPhat = Enum.TryParse<PenaltyType>(request.PenaltyType, true, out var p) ? p : PenaltyType.Warning;
            var penalty = await VenuePenaltyIssuer.IssueAsync(_uow, _notifications, _config, lounge, mucPhat,
                $"Huỷ buổi hòa nhạc \"{show.Name}\" đã mở bán với lý do không được chấp nhận: {note}",
                $"show-cancellation-review:{review.Id}", request.SuspensionDays, _currentUser.UserId, ct);
            review.Status = ShowCancellationReviewStatus.Penalized;
            review.PenaltyId = penalty.Id;
        }
        else
        {
            review.Status = ShowCancellationReviewStatus.Excused;
            await _notifications.NotifyAsync(
                lounge.OwnerId, NotificationType.ShowCancellationReview,
                new SongNgu($"Lý do huỷ \"{show.Name}\" được chấp nhận", $"Cancellation reason for \"{show.Name}\" accepted"),
                new SongNgu(
                    $"Nền tảng đã xét lý do huỷ buổi \"{show.Name}\" và không áp án phạt. Ghi chú: {note}",
                    $"The platform reviewed why \"{show.Name}\" was cancelled and will not issue a penalty. Note: {note}"),
                referenceType: "show", referenceId: show.Id.ToString(), ct: ct);
        }

        repo.Update(review);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

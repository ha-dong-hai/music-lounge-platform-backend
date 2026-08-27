using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.LoungeShows.Commands.RateShow;

internal sealed class RateShowCommandHandler : IRequestHandler<RateShowCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public RateShowCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RateShowCommand request, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        if (show.Status != LoungeShowStatus.Ended)
            throw new DomainException("Chỉ có thể đánh giá show sau khi kết thúc.");

        // §6.13: cua so danh gia 7 ngay sau khi show ket thuc. Show da Ended TRUOC khi field nay
        // ton tai se co RatingOpenUntil=null - khong chan hoi to (khong suy dien gioi han cho du
        // lieu cu khong co thoi diem ket thuc that).
        if (show.RatingOpenUntil.HasValue && DateTimeOffset.UtcNow > show.RatingOpenUntil.Value)
            throw new DomainException("Đã hết hạn đánh giá show này (7 ngày sau khi kết thúc).");

        // MLACP-140 DONE WHEN: "Không check-in không đánh giá được" — bắt buộc Status=Used, không
        // còn chấp nhận Confirmed đơn thuần (khác với local master, nơi Confirmed cũng qua được).
        // Ve vat ly: Used chi duoc set boi CheckInTicketCommandHandler khi nhan vien quet QR that o
        // cua. Ve Livestream: khong co quay nao de quet, nen duoc tu dong chuyen sang Used boi
        // CheckInLivestreamViewerJob dung luc chu ve that su nhan duoc HlsUrl phat (xem
        // GetLivestreamDetailQueryHandler) — 2 co che khac nhau nhung hoi tu ve cung 1 dieu kien.
        var hasCheckedIn = await _uow.Repository<Ticket, Guid>()
            .AnyAsync(t => t.ShowId == request.ShowId
                && t.BuyerId == _currentUser.UserId
                && t.Status == TicketStatus.Used, ct);

        if (!hasCheckedIn)
            throw new ForbiddenException("Bạn cần check-in (vào cửa hoặc xem livestream) để đánh giá show này.");

        var alreadyRated = await _uow.Repository<LoungeShowRating, int>()
            .AnyAsync(r => r.LoungeShowId == request.ShowId && r.UserId == _currentUser.UserId, ct);
        if (alreadyRated)
            throw new ConflictException("Bạn đã đánh giá show này rồi.");

        _uow.Repository<LoungeShowRating, int>().Add(new LoungeShowRating
        {
            UserId = _currentUser.UserId,
            LoungeShowId = request.ShowId,
            Score = request.Score,
            Comment = request.Comment,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

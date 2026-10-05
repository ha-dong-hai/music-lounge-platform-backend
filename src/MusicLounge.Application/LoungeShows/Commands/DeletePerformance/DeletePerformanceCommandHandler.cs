using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.DeletePerformance;

internal sealed class DeletePerformanceCommandHandler : IRequestHandler<DeletePerformanceCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;

    public DeletePerformanceCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(DeletePerformanceCommand request, CancellationToken ct)
    {
        var performanceRepo = _uow.Repository<Performance, Guid>();
        var performance = await performanceRepo.GetByIdAsync(request.PerformanceId, ct)
            ?? throw new NotFoundException(nameof(Performance), request.PerformanceId);

        var show = await _uow.Repository<LoungeShow, Guid>().GetByIdAsync(performance.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), performance.LoungeShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, Guid>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền sửa danh sách biểu diễn của event này.");

        // MLACP-622: trước đây chỉ khi còn nháp — sau khi gửi duyệt không thay được ca sĩ. Xem LineupChange.
        LineupChange.EnsureEditable(show);

        // Tiền ủng hộ gắn chặt với tiết mục (FK Restrict). Không chặn ở đây thì cơ sở dữ liệu từ chối và GlobalExceptionHandler
        // trả 409 với câu chung "Dữ liệu đã tồn tại hoặc xung đột" — chủ phòng trà không biết vì sao, cũng không biết làm gì.
        // Chặn TRƯỚC RecordAdverseChangeAsync: người mua không được nhận tin "nghệ sĩ đã đổi" cho một thay đổi không xảy ra.
        if (await _uow.Repository<Donation, Guid>().AnyAsync(d => d.PerformanceId == performance.Id, ct))
            throw new ConflictException(
                "Nghệ sĩ này đã nhận tiền ủng hộ trong buổi diễn — không xoá được khỏi danh sách vì sổ sách ủng hộ gắn với " +
                "tiết mục này. Hãy tắt nhận ủng hộ cho tiết mục thay vì xoá.");

        // Bỏ một nghệ sĩ khỏi buổi đã mở bán là thay đổi bất lợi: bắt lý do, mở cửa sổ hoàn 100%, báo người giữ vé.
        var performer = await _uow.Repository<Performer, Guid>().GetByIdAsync(performance.PerformerId, ct);
        await LineupChange.RecordAdverseChangeAsync(
            _uow, _notifications, show, performer?.Name ?? "Một nghệ sĩ", request.ChangeReason, ct);

        performanceRepo.Remove(performance);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

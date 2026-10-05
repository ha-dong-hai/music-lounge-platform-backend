using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.UpdatePerformance;

internal sealed class UpdatePerformanceCommandHandler : IRequestHandler<UpdatePerformanceCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;

    public UpdatePerformanceCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(UpdatePerformanceCommand request, CancellationToken ct)
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

        var vaiMoi = Enum.Parse<PerformerRole>(request.Role, ignoreCase: true);
        // Hạ nghệ sĩ chính xuống khách mời/dẫn chương trình: người mua đã trả tiền vì người đó là chính — bất lợi.
        if (performance.Role == PerformerRole.Main && vaiMoi != PerformerRole.Main)
        {
            var performer = await _uow.Repository<Performer, Guid>().GetByIdAsync(performance.PerformerId, ct);
            await LineupChange.RecordAdverseChangeAsync(
                _uow, _notifications, show, performer?.Name ?? "Nghệ sĩ chính", request.ChangeReason, ct);
        }
        performance.Role = vaiMoi;
        performance.OrderIndex = request.OrderIndex;
        performance.SetTime = request.SetTime;
        performance.AcceptsDonation = request.AcceptsDonation;

        performanceRepo.Update(performance);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

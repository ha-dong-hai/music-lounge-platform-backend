using MediatR;
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

    public UpdatePerformanceCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdatePerformanceCommand request, CancellationToken ct)
    {
        var performanceRepo = _uow.Repository<Performance, int>();
        var performance = await performanceRepo.GetByIdAsync(request.PerformanceId, ct)
            ?? throw new NotFoundException(nameof(Performance), request.PerformanceId);

        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(performance.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), performance.LoungeShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền sửa danh sách biểu diễn của event này.");

        if (show.Status != LoungeShowStatus.Draft)
            throw new DomainException("Chỉ có thể sửa danh sách biểu diễn khi event còn ở trạng thái Draft.");

        performance.Role = Enum.Parse<PerformerRole>(request.Role, ignoreCase: true);
        performance.OrderIndex = request.OrderIndex;
        performance.SetTime = request.SetTime;
        performance.AcceptsDonation = request.AcceptsDonation;

        performanceRepo.Update(performance);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

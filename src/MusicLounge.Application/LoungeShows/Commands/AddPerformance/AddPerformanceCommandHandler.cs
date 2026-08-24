using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.AddPerformance;

internal sealed class AddPerformanceCommandHandler : IRequestHandler<AddPerformanceCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public AddPerformanceCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(AddPerformanceCommand request, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền sửa danh sách biểu diễn của event này.");

        if (show.Status != LoungeShowStatus.Draft)
            throw new DomainException("Chỉ có thể sửa danh sách biểu diễn khi event còn ở trạng thái Draft.");

        var performerRepo = _uow.Repository<Performer, int>();
        int performerId;
        if (request.PerformerId.HasValue)
        {
            var performer = await performerRepo.GetByIdAsync(request.PerformerId.Value, ct)
                ?? throw new NotFoundException(nameof(Performer), request.PerformerId.Value);
            performerId = performer.Id;
        }
        else
        {
            var newPerformer = new Performer
            {
                Name = request.PerformerName!,
                CreatedByUserId = _currentUser.UserId,
                CreatedAt = DateTime.UtcNow
            };
            performerRepo.Add(newPerformer);
            await _uow.SaveChangesAsync(ct);
            performerId = newPerformer.Id;
        }

        // DONE WHEN: cung 1 nghe si khong duoc them 2 lan vao cung 1 event. DB co unique index
        // (LoungeShowId, PerformerId) lam luoi an toan cuoi, nhung check truoc o day de tra loi
        // 409 ro rang thay vi de DbUpdateException chung chung roi xuong 500.
        var alreadyInLineup = await _uow.Repository<Performance, int>()
            .AnyAsync(p => p.LoungeShowId == request.ShowId && p.PerformerId == performerId, ct);
        if (alreadyInLineup)
            throw new ConflictException("Nghệ sĩ này đã có trong danh sách biểu diễn của event này.");

        var role = Enum.Parse<PerformerRole>(request.Role, ignoreCase: true);
        var performance = new Performance
        {
            LoungeShowId = request.ShowId,
            PerformerId = performerId,
            Role = role,
            OrderIndex = request.OrderIndex,
            SetTime = request.SetTime,
            AcceptsDonation = request.AcceptsDonation
        };
        _uow.Repository<Performance, int>().Add(performance);
        await _uow.SaveChangesAsync(ct);

        return performance.Id;
    }
}

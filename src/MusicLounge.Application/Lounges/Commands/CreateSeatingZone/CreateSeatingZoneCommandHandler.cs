using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.CreateSeatingZone;

internal sealed class CreateSeatingZoneCommandHandler : IRequestHandler<CreateSeatingZoneCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateSeatingZoneCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateSeatingZoneCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền quản lý khu vực chỗ ngồi cho venue này.");

        var zoneRepo = _uow.Repository<SeatingZone, int>();
        var existingCount = (await zoneRepo.FindAsync(z => z.LoungeId == request.LoungeId, ct)).Count;

        var zone = new SeatingZone
        {
            LoungeId = request.LoungeId,
            Name = request.Name,
            Description = request.Description,
            Capacity = request.Capacity,
            DisplayOrder = existingCount,
            IsActive = true
        };

        zoneRepo.Add(zone);
        await _uow.SaveChangesAsync(ct);

        return zone.Id;
    }
}

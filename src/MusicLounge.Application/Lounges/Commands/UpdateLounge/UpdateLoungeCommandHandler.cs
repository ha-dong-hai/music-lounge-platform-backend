using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Domain.ValueObjects;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.UpdateLounge;

internal sealed class UpdateLoungeCommandHandler : IRequestHandler<UpdateLoungeCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UpdateLoungeCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateLoungeCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<MusicLoungeEntity, int>();
        var lounge = await repo.GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        lounge.Name = request.Name;
        lounge.Description = request.Description;
        lounge.AtmosphereId = request.AtmosphereId;
        lounge.Address = new VenueAddress
        {
            Street = request.Street,
            Ward = request.Ward,
            District = request.District,
            City = request.City,
            Latitude = request.Latitude,
            Longitude = request.Longitude
        };

        repo.Update(lounge);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

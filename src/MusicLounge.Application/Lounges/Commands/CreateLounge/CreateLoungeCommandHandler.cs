using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.ValueObjects;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.CreateLounge;

internal sealed class CreateLoungeCommandHandler : IRequestHandler<CreateLoungeCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateLoungeCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateLoungeCommand request, CancellationToken ct)
    {
        var lounge = new MusicLoungeEntity
        {
            OwnerId = _currentUser.UserId,
            Name = request.Name,
            Description = request.Description,
            AtmosphereId = request.AtmosphereId,
            Address = new VenueAddress
            {
                Street = request.Street,
                Ward = request.Ward,
                District = request.District,
                City = request.City,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            }
        };

        _uow.Repository<MusicLoungeEntity, int>().Add(lounge);
        await _uow.SaveChangesAsync(ct);

        return lounge.Id;
    }
}

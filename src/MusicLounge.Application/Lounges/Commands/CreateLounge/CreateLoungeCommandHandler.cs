using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
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
        // MLACP-374: mot chu chi duoc so huu MOT phong tra — xac nhan tu nguoi dung, he thong khong
        // thiet ke cho nhieu phong tra chung 1 chu (OwnerSubscription, ho so thue, KYC deu tinh theo
        // User). Kiem tra tuong minh o day thay vi de lo DbUpdateException chung chung tu unique index.
        var alreadyOwnsLounge = await _uow.Repository<MusicLoungeEntity, int>()
            .AnyAsync(l => l.OwnerId == _currentUser.UserId, ct);
        if (alreadyOwnsLounge)
            throw new ConflictException(
                "Bạn đã có một phòng trà — mỗi tài khoản chủ chỉ được sở hữu một phòng trà. " +
                "Hãy chỉnh sửa phòng trà hiện có thay vì tạo mới.");

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

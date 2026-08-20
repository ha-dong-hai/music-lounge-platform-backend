using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.RemoveLoungeGalleryImage;

internal sealed class RemoveLoungeGalleryImageCommandHandler : IRequestHandler<RemoveLoungeGalleryImageCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public RemoveLoungeGalleryImageCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RemoveLoungeGalleryImageCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        var imageRepo = _uow.Repository<LoungeGalleryImage, int>();
        var image = await imageRepo.GetByIdAsync(request.ImageId, ct);
        if (image is null || image.LoungeId != request.LoungeId)
            throw new NotFoundException(nameof(LoungeGalleryImage), request.ImageId);

        imageRepo.Remove(image);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

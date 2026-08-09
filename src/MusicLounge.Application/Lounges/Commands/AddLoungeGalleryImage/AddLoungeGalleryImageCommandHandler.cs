using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.AddLoungeGalleryImage;

// Free for every Owner, no subscription gate — same as PrimaryImageUrl, unlike VenueTourScene
// (the 360° tour, which IS gated) since these are just showcase photos, not an interactive feature.
internal sealed class AddLoungeGalleryImageCommandHandler : IRequestHandler<AddLoungeGalleryImageCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public AddLoungeGalleryImageCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(AddLoungeGalleryImageCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        var imageRepo = _uow.Repository<LoungeGalleryImage, int>();
        var existingCount = (await imageRepo.FindAsync(g => g.LoungeId == request.LoungeId, ct)).Count;

        var image = new LoungeGalleryImage
        {
            LoungeId = request.LoungeId,
            ImageUrl = request.ImageUrl,
            Caption = request.Caption,
            OrderIndex = existingCount
        };
        imageRepo.Add(image);
        await _uow.SaveChangesAsync(ct);
        return image.Id;
    }
}

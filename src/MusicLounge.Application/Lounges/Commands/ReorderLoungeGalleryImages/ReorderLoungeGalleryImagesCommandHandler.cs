using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.ReorderLoungeGalleryImages;

internal sealed class ReorderLoungeGalleryImagesCommandHandler
    : IRequestHandler<ReorderLoungeGalleryImagesCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public ReorderLoungeGalleryImagesCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(ReorderLoungeGalleryImagesCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        var imageRepo = _uow.Repository<LoungeGalleryImage, int>();
        var images = await imageRepo.FindAsync(g => g.LoungeId == request.LoungeId, ct);

        // OrderedImageIds phai la hoan vi day du cua anh hien co - khong cho thieu/du/lac sang
        // phong tra khac, tranh sap xep sai am tham hoac lo mat 1 anh khoi thu tu hien thi.
        var existingIds = images.Select(i => i.Id).ToHashSet();
        var requestedIds = request.OrderedImageIds.ToHashSet();
        if (!existingIds.SetEquals(requestedIds))
            throw new DomainException("Danh sách ảnh không khớp với ảnh hiện có của phòng trà này.");

        var imagesById = images.ToDictionary(i => i.Id);
        for (var index = 0; index < request.OrderedImageIds.Count; index++)
        {
            var image = imagesById[request.OrderedImageIds[index]];
            image.OrderIndex = index;
            imageRepo.Update(image);
        }

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

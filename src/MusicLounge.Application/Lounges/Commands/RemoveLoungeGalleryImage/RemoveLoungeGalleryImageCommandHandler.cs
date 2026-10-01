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

        // MLACP-506. Chiều ngược của MLACP-33 ("ảnh gallery đầu tiên tự là ảnh đại diện"): xoá đúng ảnh đang làm đại diện
        // thì ảnh đại diện phải đi theo — trước đây nó vẫn trỏ vào ảnh vừa xoá (file đã mất thì thành ô ảnh vỡ trên thẻ
        // phòng trà), và không có đường nào để gỡ. Chuyển sang ảnh gallery kế tiếp theo thứ tự hiển thị; hết ảnh thì để
        // trống. Ảnh đại diện đặt riêng (PUT /image, không thuộc gallery) không bị đụng tới.
        if (lounge.PrimaryImageUrl == image.ImageUrl)
        {
            var conLai = (await imageRepo.FindAsync(g => g.LoungeId == request.LoungeId && g.Id != image.Id, ct))
                .OrderBy(g => g.OrderIndex).ThenBy(g => g.Id)
                .FirstOrDefault();
            lounge.PrimaryImageUrl = conLai?.ImageUrl;
            _uow.Repository<MusicLoungeEntity, int>().Update(lounge);
        }

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

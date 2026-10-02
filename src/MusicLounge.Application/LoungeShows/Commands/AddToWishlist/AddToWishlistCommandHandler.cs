using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.LoungeShows.Commands.AddToWishlist;

internal sealed class AddToWishlistCommandHandler : IRequestHandler<AddToWishlistCommand, Unit>
{
    private readonly IRepository<ShowWishlist, Guid> _wishlistRepo;
    private readonly IRepository<LoungeShow, Guid> _showRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public AddToWishlistCommandHandler(
        IRepository<ShowWishlist, Guid> wishlistRepo,
        IRepository<LoungeShow, Guid> showRepo,
        ICurrentUserService currentUser,
        IUnitOfWork uow)
    {
        _wishlistRepo = wishlistRepo;
        _showRepo = showRepo;
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Unit> Handle(AddToWishlistCommand request, CancellationToken ct)
    {
        // Buoi chua cong bo (nhap / dang cho duyet) tra 404 y nhu khong ton tai: truoc day chi loai Draft,
        // nen ai doan duoc ma so cua mot buoi Pending thi luu duoc no vao danh sach yeu thich va doc
        // ten, anh, gio dien cua buoi chua duyet qua GET /users/me/wishlist.
        var showExists = await _showRepo.AnyAsync(
            s => s.Id == request.ShowId
                && !ShowDiscoverability.AwaitingPublication.Contains(s.Status)
                && s.Status != LoungeShowStatus.Cancelled, ct);
        if (!showExists)
            throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var alreadyWishlisted = await _wishlistRepo.AnyAsync(
            w => w.UserId == _currentUser.UserId && w.LoungeShowId == request.ShowId, ct);

        if (alreadyWishlisted)
            throw new ConflictException("LoungeShow đã có trong danh sách yêu thích.");

        _wishlistRepo.Add(new ShowWishlist
        {
            UserId = _currentUser.UserId,
            LoungeShowId = request.ShowId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

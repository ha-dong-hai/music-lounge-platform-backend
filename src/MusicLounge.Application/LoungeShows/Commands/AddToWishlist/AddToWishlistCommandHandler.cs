using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.LoungeShows.Commands.AddToWishlist;

internal sealed class AddToWishlistCommandHandler : IRequestHandler<AddToWishlistCommand, Unit>
{
    private readonly IRepository<ShowWishlist, int> _wishlistRepo;
    private readonly IRepository<LoungeShow, int> _showRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public AddToWishlistCommandHandler(
        IRepository<ShowWishlist, int> wishlistRepo,
        IRepository<LoungeShow, int> showRepo,
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
        var showExists = await _showRepo.AnyAsync(
            s => s.Id == request.ShowId
                && s.Status != LoungeShowStatus.Draft
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

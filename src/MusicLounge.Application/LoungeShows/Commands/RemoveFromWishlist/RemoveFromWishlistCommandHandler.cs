using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.LoungeShows.Commands.RemoveFromWishlist;

internal sealed class RemoveFromWishlistCommandHandler : IRequestHandler<RemoveFromWishlistCommand, Unit>
{
    private readonly IRepository<ShowWishlist, int> _wishlistRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public RemoveFromWishlistCommandHandler(
        IRepository<ShowWishlist, int> wishlistRepo,
        ICurrentUserService currentUser,
        IUnitOfWork uow)
    {
        _wishlistRepo = wishlistRepo;
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Unit> Handle(RemoveFromWishlistCommand request, CancellationToken ct)
    {
        var entry = (await _wishlistRepo.FindAsync(
            w => w.UserId == _currentUser.UserId && w.LoungeShowId == request.ShowId, ct))
            .FirstOrDefault()
            ?? throw new NotFoundException("Wishlist entry", request.ShowId);

        _wishlistRepo.Remove(entry);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

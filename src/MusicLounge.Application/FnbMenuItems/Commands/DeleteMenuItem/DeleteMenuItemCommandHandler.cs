using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.FnbMenuItems.Commands.DeleteMenuItem;

internal sealed class DeleteMenuItemCommandHandler : IRequestHandler<DeleteMenuItemCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public DeleteMenuItemCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteMenuItemCommand request, CancellationToken ct)
    {
        var itemRepo = _uow.Repository<FnbMenuItem, int>();
        var item = await itemRepo.GetByIdAsync(request.MenuItemId, ct)
            ?? throw new NotFoundException(nameof(FnbMenuItem), request.MenuItemId);

        var menu = await _uow.Repository<FnbMenu, int>().GetByIdAsync(item.MenuId, ct)
            ?? throw new NotFoundException(nameof(FnbMenu), item.MenuId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(menu.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), menu.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền quản lý menu cho venue này.");

        // OrderItemConfiguration restricts MenuItemId at the DB level (never cascades) so historical
        // orders always keep their snapshotted UnitPrice/Name intact — check explicitly here for a
        // clean 409 instead of letting SaveChangesAsync surface a raw DbUpdateException.
        var hasBeenOrdered = await _uow.Repository<OrderItem, int>()
            .AnyAsync(o => o.MenuItemId == request.MenuItemId, ct);
        if (hasBeenOrdered)
            throw new ConflictException(
                "Món này đã có trong đơn hàng — không thể xoá. Hãy ẩn món thay vì xoá.");

        itemRepo.Remove(item);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

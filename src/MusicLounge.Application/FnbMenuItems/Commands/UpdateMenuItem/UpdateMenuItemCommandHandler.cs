using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.FnbMenuItems.Commands.UpdateMenuItem;

internal sealed class UpdateMenuItemCommandHandler : IRequestHandler<UpdateMenuItemCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UpdateMenuItemCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateMenuItemCommand request, CancellationToken ct)
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

        item.Category = request.Category;
        item.Name = request.Name;
        item.Description = request.Description;
        item.Price = request.Price;
        item.ImageUrl = request.ImageUrl;
        item.IsAvailable = request.IsAvailable;
        item.DisplayOrder = request.DisplayOrder;

        itemRepo.Update(item);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

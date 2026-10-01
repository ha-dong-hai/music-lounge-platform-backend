using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.FnbMenuItems.Commands.CreateMenuItem;

internal sealed class CreateMenuItemCommandHandler : IRequestHandler<CreateMenuItemCommand, Guid>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateMenuItemCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateMenuItemCommand request, CancellationToken ct)
    {
        var menu = await _uow.Repository<FnbMenu, Guid>().GetByIdAsync(request.MenuId, ct)
            ?? throw new NotFoundException(nameof(FnbMenu), request.MenuId);

        var lounge = await _uow.Repository<MusicLoungeEntity, Guid>().GetByIdAsync(menu.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), menu.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền quản lý menu cho venue này.");

        var item = new FnbMenuItem
        {
            MenuId = request.MenuId,
            Category = request.Category,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            ImageUrl = request.ImageUrl,
            DisplayOrder = request.DisplayOrder
        };

        _uow.Repository<FnbMenuItem, Guid>().Add(item);
        await _uow.SaveChangesAsync(ct);

        return item.Id;
    }
}

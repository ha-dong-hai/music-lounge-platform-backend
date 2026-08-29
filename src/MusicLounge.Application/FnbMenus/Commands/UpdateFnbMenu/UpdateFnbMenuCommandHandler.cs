using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.FnbMenus.Commands.UpdateFnbMenu;

internal sealed class UpdateFnbMenuCommandHandler : IRequestHandler<UpdateFnbMenuCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UpdateFnbMenuCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateFnbMenuCommand request, CancellationToken ct)
    {
        var menuRepo = _uow.Repository<FnbMenu, int>();
        var menu = await menuRepo.GetByIdAsync(request.MenuId, ct)
            ?? throw new NotFoundException(nameof(FnbMenu), request.MenuId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(menu.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), menu.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền quản lý menu cho venue này.");

        menu.Name = request.Name;
        menu.Description = request.Description;
        menu.IsActive = request.IsActive;
        menu.DisplayOrder = request.DisplayOrder;

        menuRepo.Update(menu);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.FnbMenus.Commands.CreateFnbMenu;

internal sealed class CreateFnbMenuCommandHandler : IRequestHandler<CreateFnbMenuCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateFnbMenuCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateFnbMenuCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền quản lý menu cho venue này.");

        var menu = new FnbMenu
        {
            LoungeId = request.LoungeId,
            Name = request.Name,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder
        };

        _uow.Repository<FnbMenu, int>().Add(menu);
        await _uow.SaveChangesAsync(ct);

        return menu.Id;
    }
}

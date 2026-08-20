using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Performers.Commands.RemovePerformerSocialLink;

internal sealed class RemovePerformerSocialLinkCommandHandler : IRequestHandler<RemovePerformerSocialLinkCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public RemovePerformerSocialLinkCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RemovePerformerSocialLinkCommand request, CancellationToken ct)
    {
        var performer = await _uow.Repository<Performer, int>().GetByIdAsync(request.PerformerId, ct)
            ?? throw new NotFoundException(nameof(Performer), request.PerformerId);

        if (performer.CreatedByUserId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Chỉ người tạo hồ sơ nghệ sĩ này hoặc Admin mới có quyền sửa.");

        var linkRepo = _uow.Repository<PerformerSocialLink, int>();
        var link = await linkRepo.GetByIdAsync(request.LinkId, ct);
        if (link is null || link.PerformerId != request.PerformerId)
            throw new NotFoundException(nameof(PerformerSocialLink), request.LinkId);

        linkRepo.Remove(link);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

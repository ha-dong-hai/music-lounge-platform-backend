using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Performers.Commands.AddPerformerSocialLink;

// §6.14 — upsert semantics: setting a link for a platform the performer already has replaces it
// rather than creating a duplicate (matches the unique index on (PerformerId, Platform) and the
// natural UX of "set your Spotify link" being a single field, not a list to manage).
// §6.12 edit rights apply here too — only the performer's creator or Admin may add/change links.
internal sealed class AddPerformerSocialLinkCommandHandler : IRequestHandler<AddPerformerSocialLinkCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public AddPerformerSocialLinkCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(AddPerformerSocialLinkCommand request, CancellationToken ct)
    {
        var performer = await _uow.Repository<Performer, int>().GetByIdAsync(request.PerformerId, ct)
            ?? throw new NotFoundException(nameof(Performer), request.PerformerId);

        if (performer.CreatedByUserId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Chỉ người tạo hồ sơ nghệ sĩ này hoặc Admin mới có quyền sửa.");

        var platform = Enum.Parse<SocialPlatform>(request.Platform, ignoreCase: true);
        var linkRepo = _uow.Repository<PerformerSocialLink, int>();

        var existing = (await linkRepo.FindAsync(
            l => l.PerformerId == request.PerformerId && l.Platform == platform, ct)).FirstOrDefault();

        if (existing is not null)
        {
            existing.Url = request.Url;
            existing.DisplayName = request.DisplayName;
            linkRepo.Update(existing);
            await _uow.SaveChangesAsync(ct);
            return existing.Id;
        }

        var link = new PerformerSocialLink
        {
            PerformerId = request.PerformerId,
            Platform = platform,
            Url = request.Url,
            DisplayName = request.DisplayName
        };
        linkRepo.Add(link);
        await _uow.SaveChangesAsync(ct);
        return link.Id;
    }
}

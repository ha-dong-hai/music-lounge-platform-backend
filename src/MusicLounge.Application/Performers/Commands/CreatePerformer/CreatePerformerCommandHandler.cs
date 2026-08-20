using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Performers.Commands.CreatePerformer;

// §6.12: Performers is a shared catalog across all Owners (not scoped to one venue) — any Owner
// can create a new profile when autocomplete (GetPerformersQuery) doesn't find an existing match.
internal sealed class CreatePerformerCommandHandler : IRequestHandler<CreatePerformerCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreatePerformerCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreatePerformerCommand request, CancellationToken ct)
    {
        var performer = new Performer
        {
            Name = request.Name,
            AvatarUrl = request.AvatarUrl,
            Bio = request.Bio,
            Type = Enum.Parse<PerformerType>(request.Type, ignoreCase: true),
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };
        _uow.Repository<Performer, int>().Add(performer);
        await _uow.SaveChangesAsync(ct);

        var genreRepo = _uow.Repository<PerformerGenre, int>();
        foreach (var genreId in request.GenreIds.Distinct())
            genreRepo.Add(new PerformerGenre { PerformerId = performer.Id, GenreId = genreId });

        await _uow.SaveChangesAsync(ct);
        return performer.Id;
    }
}

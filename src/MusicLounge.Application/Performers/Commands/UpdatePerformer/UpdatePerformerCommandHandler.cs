using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Performers.Commands.UpdatePerformer;

// §6.12: EDIT rights are restricted to created_by_user_id + Admin — unlike CREATE/READ/ASSIGN,
// which are open to every Owner, since the catalog is shared and an unrestricted edit would let
// any Owner silently rewrite another Owner's performer profile out from under them.
internal sealed class UpdatePerformerCommandHandler : IRequestHandler<UpdatePerformerCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UpdatePerformerCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdatePerformerCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<Performer, int>();
        var performer = await repo.GetByIdAsync(request.PerformerId, ct)
            ?? throw new NotFoundException(nameof(Performer), request.PerformerId);

        if (performer.CreatedByUserId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Chỉ người tạo hồ sơ nghệ sĩ này hoặc Admin mới có quyền sửa.");

        performer.Name = request.Name;
        performer.AvatarUrl = request.AvatarUrl;
        performer.Bio = request.Bio;
        performer.Type = Enum.Parse<PerformerType>(request.Type, ignoreCase: true);
        repo.Update(performer);
        await _uow.SaveChangesAsync(ct);

        // Remove-then-add in two separate SaveChangesAsync calls: EF Core doesn't guarantee a
        // DELETE for an old (PerformerId, GenreId) pair commits before an INSERT for the same pair
        // within a single SaveChangesAsync, which can transiently violate PerformerGenreConfiguration's
        // unique index on that pair even when the net result (same genre kept) is a no-op change.
        var genreRepo = _uow.Repository<PerformerGenre, int>();
        var existingGenres = await genreRepo.FindAsync(g => g.PerformerId == performer.Id, ct);
        if (existingGenres.Count > 0)
        {
            foreach (var existing in existingGenres)
                genreRepo.Remove(existing);
            await _uow.SaveChangesAsync(ct);
        }

        foreach (var genreId in request.GenreIds.Distinct())
            genreRepo.Add(new PerformerGenre { PerformerId = performer.Id, GenreId = genreId });

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

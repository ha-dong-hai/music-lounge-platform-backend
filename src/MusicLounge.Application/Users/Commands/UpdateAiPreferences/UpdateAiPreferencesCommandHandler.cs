using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.UpdateAiPreferences;

internal sealed class UpdateAiPreferencesCommandHandler : IRequestHandler<UpdateAiPreferencesCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IBackgroundJobService _backgroundJobs;

    public UpdateAiPreferencesCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IBackgroundJobService backgroundJobs)
    {
        _uow = uow;
        _currentUser = currentUser;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<Unit> Handle(UpdateAiPreferencesCommand request, CancellationToken ct)
    {
        var user = await _uow.Repository<User, int>().GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        // Validate all IDs in a single query each before making any changes
        var genreIds = request.GenreIds.Distinct().ToList();
        if (genreIds.Count > 0)
        {
            var foundGenres = await _uow.Repository<MusicGenre, int>()
                .CountAsync(g => genreIds.Contains(g.Id), ct);
            if (foundGenres != genreIds.Count)
                throw new NotFoundException(nameof(MusicGenre), "một hoặc nhiều genre không tồn tại.");
        }

        var moodIds = request.MoodIds.Distinct().ToList();
        if (moodIds.Count > 0)
        {
            var foundMoods = await _uow.Repository<Mood, int>()
                .CountAsync(m => moodIds.Contains(m.Id), ct);
            if (foundMoods != moodIds.Count)
                throw new NotFoundException(nameof(Mood), "một hoặc nhiều mood không tồn tại.");
        }

        var atmosphereIds = request.AtmosphereIds.Distinct().ToList();
        if (atmosphereIds.Count > 0)
        {
            var foundAtmospheres = await _uow.Repository<VenueAtmosphere, int>()
                .CountAsync(a => atmosphereIds.Contains(a.Id), ct);
            if (foundAtmospheres != atmosphereIds.Count)
                throw new NotFoundException(nameof(VenueAtmosphere), "một hoặc nhiều atmosphere không tồn tại.");
        }

        // Remove-then-add for each preference set is split into two SaveChangesAsync calls (remove
        // batch flushed first): EF Core doesn't guarantee a DELETE for an old (UserId, XId) pair
        // commits before an INSERT for the same pair within one SaveChangesAsync, which can
        // transiently violate the unique index on that pair even when the net result (same tag
        // kept across an update) is a no-op change — same class of bug fixed earlier for
        // BankAccount/PerformerGenre.
        var existingGenres = await _uow.Repository<UserFavouriteGenre, int>()
            .FindAsync(g => g.UserId == _currentUser.UserId, ct);
        var existingMoods = await _uow.Repository<UserFavouriteMood, int>()
            .FindAsync(m => m.UserId == _currentUser.UserId, ct);
        var existingAtmospheres = await _uow.Repository<UserFavouriteAtmosphere, int>()
            .FindAsync(a => a.UserId == _currentUser.UserId, ct);

        foreach (var g in existingGenres) _uow.Repository<UserFavouriteGenre, int>().Remove(g);
        foreach (var m in existingMoods) _uow.Repository<UserFavouriteMood, int>().Remove(m);
        foreach (var a in existingAtmospheres) _uow.Repository<UserFavouriteAtmosphere, int>().Remove(a);

        if (existingGenres.Count > 0 || existingMoods.Count > 0 || existingAtmospheres.Count > 0)
            await _uow.SaveChangesAsync(ct);

        foreach (var genreId in genreIds)
            _uow.Repository<UserFavouriteGenre, int>().Add(new UserFavouriteGenre
            {
                UserId = _currentUser.UserId,
                GenreId = genreId
            });
        foreach (var moodId in moodIds)
            _uow.Repository<UserFavouriteMood, int>().Add(new UserFavouriteMood
            {
                UserId = _currentUser.UserId,
                MoodId = moodId
            });
        foreach (var atmosphereId in atmosphereIds)
            _uow.Repository<UserFavouriteAtmosphere, int>().Add(new UserFavouriteAtmosphere
            {
                UserId = _currentUser.UserId,
                AtmosphereId = atmosphereId
            });

        user.AiConsent = request.EnableAiConsent;
        _uow.Repository<User, int>().Update(user);

        await _uow.SaveChangesAsync(ct);

        // MLACP-130: khong lam gi thi cache goi y cu (RecommendedLoungeShow, TTL toi 6h -
        // RefreshUserRecommendationJob) van con hieu luc, khien "cap nhat so thich" khong tao ra
        // tac dung gi cho toi khi cache tu het han. TriggerRecommendationRefreshAsync doc lai
        // favourite genres/moods/atmospheres MOI NHAT tu DB tai thoi diem chay job (sau dong
        // SaveChangesAsync o tren), roi xoa het cache cu truoc khi ghi ket qua moi
        // (PersistRecommendationsAsync) - nen enqueue sau khi luu la du, khong can xoa cache thu
        // cong o day.
        _backgroundJobs.EnqueueRecommendationRefresh(_currentUser.UserId);

        return Unit.Value;
    }
}

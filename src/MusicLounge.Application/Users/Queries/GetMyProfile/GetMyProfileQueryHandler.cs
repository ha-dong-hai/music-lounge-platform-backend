using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Queries.GetMyProfile;

internal sealed class GetMyProfileQueryHandler
    : IRequestHandler<GetMyProfileQuery, UserProfileDto>
{
    private readonly IRepository<User, Guid> _userRepo;
    private readonly IRepository<UserFavouriteGenre, Guid> _favGenreRepo;
    private readonly IRepository<UserFavouriteMood, Guid> _favMoodRepo;
    private readonly IRepository<UserFavouriteAtmosphere, Guid> _favAtmosphereRepo;
    private readonly IRepository<UserDislikedGenre, Guid> _dislikedGenreRepo;
    private readonly ICurrentUserService _currentUser;

    public GetMyProfileQueryHandler(
        IRepository<User, Guid> userRepo,
        IRepository<UserFavouriteGenre, Guid> favGenreRepo,
        IRepository<UserFavouriteMood, Guid> favMoodRepo,
        IRepository<UserFavouriteAtmosphere, Guid> favAtmosphereRepo,
        IRepository<UserDislikedGenre, Guid> dislikedGenreRepo,
        ICurrentUserService currentUser)
    {
        _userRepo = userRepo;
        _favGenreRepo = favGenreRepo;
        _favMoodRepo = favMoodRepo;
        _favAtmosphereRepo = favAtmosphereRepo;
        _dislikedGenreRepo = dislikedGenreRepo;
        _currentUser = currentUser;
    }

    public async Task<UserProfileDto> Handle(GetMyProfileQuery request, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        var favGenres = await _favGenreRepo.FindAsync(g => g.UserId == _currentUser.UserId, ct);
        var favMoods = await _favMoodRepo.FindAsync(m => m.UserId == _currentUser.UserId, ct);
        var favAtmospheres = await _favAtmosphereRepo.FindAsync(a => a.UserId == _currentUser.UserId, ct);
        var dislikedGenres = await _dislikedGenreRepo.FindAsync(g => g.UserId == _currentUser.UserId, ct);

        return new UserProfileDto(
            user.Id, user.FullName, user.Email, user.Phone, user.PhoneVerified, user.AvatarUrl, user.AiConsent,
            favGenres.Select(g => g.GenreId).ToList(),
            favMoods.Select(m => m.MoodId).ToList(),
            favAtmospheres.Select(a => a.AtmosphereId).ToList(),
            dislikedGenres.Select(g => g.GenreId).ToList(),
            user.PreferredLanguage);
    }
}

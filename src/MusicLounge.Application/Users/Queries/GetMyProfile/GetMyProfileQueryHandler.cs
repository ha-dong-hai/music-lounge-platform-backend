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
    private readonly IRepository<User, int> _userRepo;
    private readonly IRepository<UserFavouriteGenre, int> _favGenreRepo;
    private readonly IRepository<UserFavouriteMood, int> _favMoodRepo;
    private readonly IRepository<UserFavouriteAtmosphere, int> _favAtmosphereRepo;
    private readonly ICurrentUserService _currentUser;

    public GetMyProfileQueryHandler(
        IRepository<User, int> userRepo,
        IRepository<UserFavouriteGenre, int> favGenreRepo,
        IRepository<UserFavouriteMood, int> favMoodRepo,
        IRepository<UserFavouriteAtmosphere, int> favAtmosphereRepo,
        ICurrentUserService currentUser)
    {
        _userRepo = userRepo;
        _favGenreRepo = favGenreRepo;
        _favMoodRepo = favMoodRepo;
        _favAtmosphereRepo = favAtmosphereRepo;
        _currentUser = currentUser;
    }

    public async Task<UserProfileDto> Handle(GetMyProfileQuery request, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        var favGenres = await _favGenreRepo.FindAsync(g => g.UserId == _currentUser.UserId, ct);
        var favMoods = await _favMoodRepo.FindAsync(m => m.UserId == _currentUser.UserId, ct);
        var favAtmospheres = await _favAtmosphereRepo.FindAsync(a => a.UserId == _currentUser.UserId, ct);

        return new UserProfileDto(
            user.Id, user.FullName, user.Email, user.AvatarUrl, user.AiConsent,
            favGenres.Select(g => g.GenreId).ToList(),
            favMoods.Select(m => m.MoodId).ToList(),
            favAtmospheres.Select(a => a.AtmosphereId).ToList());
    }
}

using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.LoungeShows.Queries.GetFilterOptions;

internal sealed class GetFilterOptionsQueryHandler
    : IRequestHandler<GetFilterOptionsQuery, FilterOptionsDto>
{
    private readonly IRepository<MusicGenre, int> _genreRepo;
    private readonly IRepository<Mood, int> _moodRepo;
    private readonly IRepository<VenueAtmosphere, int> _atmosphereRepo;
    private readonly ILoungeShowRepository _showRepo;

    public GetFilterOptionsQueryHandler(
        IRepository<MusicGenre, int> genreRepo,
        IRepository<Mood, int> moodRepo,
        IRepository<VenueAtmosphere, int> atmosphereRepo,
        ILoungeShowRepository showRepo)
    {
        _genreRepo = genreRepo;
        _moodRepo = moodRepo;
        _atmosphereRepo = atmosphereRepo;
        _showRepo = showRepo;
    }

    public async Task<FilterOptionsDto> Handle(
        GetFilterOptionsQuery request, CancellationToken ct)
    {
        var genres = await _genreRepo.GetAllAsync(ct);
        var moods = await _moodRepo.GetAllAsync(ct);
        var atmospheres = await _atmosphereRepo.GetAllAsync(ct);
        var cities = await _showRepo.GetDistinctCitiesAsync(ct);

        return new FilterOptionsDto(
            genres.Select(g => new GenreDto(g.Id, g.Name)).ToList(),
            moods.Select(m => new MoodDto(m.Id, m.Name)).ToList(),
            atmospheres.Select(a => new AtmosphereDto(a.Id, a.Name)).ToList(),
            cities);
    }
}

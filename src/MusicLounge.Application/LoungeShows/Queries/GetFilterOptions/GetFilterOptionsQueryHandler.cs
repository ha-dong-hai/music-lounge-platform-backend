using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.LoungeShows.Queries.GetFilterOptions;

internal sealed class GetFilterOptionsQueryHandler
    : IRequestHandler<GetFilterOptionsQuery, FilterOptionsDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ILoungeShowRepository _showRepo;

    public GetFilterOptionsQueryHandler(IUnitOfWork uow, ILoungeShowRepository showRepo)
    {
        _uow = uow;
        _showRepo = showRepo;
    }

    public async Task<FilterOptionsDto> Handle(GetFilterOptionsQuery request, CancellationToken ct)
    {
        var genres = await _uow.Repository<MusicGenre, int>().GetAllAsync(ct);
        var moods = await _uow.Repository<Mood, int>().GetAllAsync(ct);
        var atmospheres = await _uow.Repository<VenueAtmosphere, int>().GetAllAsync(ct);

        // GetDistinctCitiesAsync đã tồn tại trong repository từ lâu và chưa từng được gọi.
        var cities = await _showRepo.GetDistinctCitiesAsync(ct);

        return new FilterOptionsDto(
            genres.Select(g => new GenreDto(g.Id, g.Name)).ToList(),
            moods.Select(m => new MoodDto(m.Id, m.Name)).ToList(),
            atmospheres.Select(a => new AtmosphereDto(a.Id, a.Name)).ToList(),
            cities);
    }
}

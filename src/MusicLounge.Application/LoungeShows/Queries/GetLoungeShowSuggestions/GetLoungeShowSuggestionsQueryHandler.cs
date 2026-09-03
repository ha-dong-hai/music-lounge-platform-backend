using MediatR;
using MusicLounge.Application.Common.Interfaces.Repositories;

namespace MusicLounge.Application.LoungeShows.Queries.GetLoungeShowSuggestions;

internal sealed class GetLoungeShowSuggestionsQueryHandler
    : IRequestHandler<GetLoungeShowSuggestionsQuery, IReadOnlyList<LoungeShowSuggestionItem>>
{
    private readonly ILoungeShowRepository _showRepo;

    public GetLoungeShowSuggestionsQueryHandler(ILoungeShowRepository showRepo)
        => _showRepo = showRepo;

    public Task<IReadOnlyList<LoungeShowSuggestionItem>> Handle(
        GetLoungeShowSuggestionsQuery request, CancellationToken ct)
        // Same missing-clamp gap as GetTrendingLoungeShowsQueryHandler — see its comment.
        => _showRepo.GetSuggestionsAsync(request.Keyword, Math.Clamp(request.Limit, 1, 20), ct);
}

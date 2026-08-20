using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Interfaces.Repositories;

namespace MusicLounge.Application.LoungeShows.Queries.GetLoungeShowSuggestions;

public sealed record GetLoungeShowSuggestionsQuery(string Keyword, int Limit = 8)
    : IQuery<IReadOnlyList<LoungeShowSuggestionItem>>;

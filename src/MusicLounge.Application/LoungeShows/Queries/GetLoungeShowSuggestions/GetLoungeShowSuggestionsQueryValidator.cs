using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Queries.GetLoungeShowSuggestions;

public sealed class GetLoungeShowSuggestionsQueryValidator : AbstractValidator<GetLoungeShowSuggestionsQuery>
{
    public GetLoungeShowSuggestionsQueryValidator()
    {
        // Not adding NotEmpty() — an empty keyword's actual query behavior is unchanged by this
        // fix and out of scope for it; only capping pathologically long input.
        RuleFor(x => x.Keyword)
            .MaximumLength(200);
    }
}

using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;

public sealed class SearchLoungeShowsQueryValidator : AbstractValidator<SearchLoungeShowsQuery>
{
    public SearchLoungeShowsQueryValidator()
    {
        RuleFor(x => x.Keyword)
            .MaximumLength(200)
            .When(x => x.Keyword is not null);
    }
}

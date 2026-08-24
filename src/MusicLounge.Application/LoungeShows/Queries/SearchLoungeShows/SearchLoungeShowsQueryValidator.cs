using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;

// Page/PageSize khong validate loi ma clamp phong thu trong handler — 1 gia tri
// page/pageSize sai khong nen tra 400, chi can tu dong sua ve gia tri hop le.
public sealed class SearchLoungeShowsQueryValidator : AbstractValidator<SearchLoungeShowsQuery>
{
    public SearchLoungeShowsQueryValidator()
    {
        RuleFor(x => x.Keyword)
            .MaximumLength(200)
            .When(x => x.Keyword is not null);
    }
}

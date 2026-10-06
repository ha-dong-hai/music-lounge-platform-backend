using FluentValidation;
using MusicLounge.Application.Common;

namespace MusicLounge.Application.Performers.Queries.GetPerformerSuggestions;

public sealed class GetPerformerSuggestionsQueryValidator : AbstractValidator<GetPerformerSuggestionsQuery>
{
    public GetPerformerSuggestionsQueryValidator()
    {
        RuleFor(x => x.Keyword).MaximumLength(SearchKeyword.MaxLength);
    }
}

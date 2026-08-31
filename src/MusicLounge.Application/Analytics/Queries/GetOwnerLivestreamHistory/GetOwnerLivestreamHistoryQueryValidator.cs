using FluentValidation;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerLivestreamHistory;

public sealed class GetOwnerLivestreamHistoryQueryValidator : AbstractValidator<GetOwnerLivestreamHistoryQuery>
{
    public GetOwnerLivestreamHistoryQueryValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
    }
}

using FluentValidation;

namespace MusicLounge.Application.Analytics.Queries.GetAudienceEngagementStats;

public sealed class GetAudienceEngagementStatsQueryValidator : AbstractValidator<GetAudienceEngagementStatsQuery>
{
    public GetAudienceEngagementStatsQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.From <= x.To)
            .WithMessage("'From' phải trước hoặc bằng 'To'.");
    }
}

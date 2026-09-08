using FluentValidation;

namespace MusicLounge.Application.Analytics.Queries.GetAiRecommendationPerformance;

public sealed class GetAiRecommendationPerformanceQueryValidator
    : AbstractValidator<GetAiRecommendationPerformanceQuery>
{
    public GetAiRecommendationPerformanceQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.From <= x.To)
            .WithMessage("'From' phải trước hoặc bằng 'To'.");
    }
}

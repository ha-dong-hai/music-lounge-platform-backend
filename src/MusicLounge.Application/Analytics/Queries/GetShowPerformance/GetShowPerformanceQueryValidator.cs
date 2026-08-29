using FluentValidation;

namespace MusicLounge.Application.Analytics.Queries.GetShowPerformance;

public sealed class GetShowPerformanceQueryValidator : AbstractValidator<GetShowPerformanceQuery>
{
    public GetShowPerformanceQueryValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0).WithMessage("ShowId không hợp lệ.");
    }
}

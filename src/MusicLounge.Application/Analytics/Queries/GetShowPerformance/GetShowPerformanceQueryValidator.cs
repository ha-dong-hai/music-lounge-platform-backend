using FluentValidation;

namespace MusicLounge.Application.Analytics.Queries.GetShowPerformance;

public sealed class GetShowPerformanceQueryValidator : AbstractValidator<GetShowPerformanceQuery>
{
    public GetShowPerformanceQueryValidator()
    {
        RuleFor(x => x.ShowId).NotEmpty().WithMessage("ShowId không hợp lệ.");
    }
}

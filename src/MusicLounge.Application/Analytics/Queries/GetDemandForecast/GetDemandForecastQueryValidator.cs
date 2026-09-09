using FluentValidation;

namespace MusicLounge.Application.Analytics.Queries.GetDemandForecast;

public sealed class GetDemandForecastQueryValidator : AbstractValidator<GetDemandForecastQuery>
{
    public GetDemandForecastQueryValidator()
        => RuleFor(x => x.ShowId).GreaterThan(0).WithMessage("ShowId không hợp lệ.");
}

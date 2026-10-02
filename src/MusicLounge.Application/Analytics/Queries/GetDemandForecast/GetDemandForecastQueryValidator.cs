using FluentValidation;

namespace MusicLounge.Application.Analytics.Queries.GetDemandForecast;

public sealed class GetDemandForecastQueryValidator : AbstractValidator<GetDemandForecastQuery>
{
    public GetDemandForecastQueryValidator()
        => RuleFor(x => x.ShowId).NotEmpty().WithMessage("ShowId không hợp lệ.");
}

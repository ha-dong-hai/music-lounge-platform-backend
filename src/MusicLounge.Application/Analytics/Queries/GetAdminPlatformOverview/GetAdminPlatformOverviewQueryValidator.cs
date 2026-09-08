using FluentValidation;

namespace MusicLounge.Application.Analytics.Queries.GetAdminPlatformOverview;

public sealed class GetAdminPlatformOverviewQueryValidator : AbstractValidator<GetAdminPlatformOverviewQuery>
{
    public GetAdminPlatformOverviewQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.From <= x.To)
            .WithMessage("'From' phải trước hoặc bằng 'To'.");
    }
}

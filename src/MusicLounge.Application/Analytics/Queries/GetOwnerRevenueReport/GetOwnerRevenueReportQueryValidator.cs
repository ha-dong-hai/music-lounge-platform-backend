using FluentValidation;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerRevenueReport;

public sealed class GetOwnerRevenueReportQueryValidator : AbstractValidator<GetOwnerRevenueReportQuery>
{
    public GetOwnerRevenueReportQueryValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0).WithMessage("LoungeId không hợp lệ.");
        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.From <= x.To)
            .WithMessage("'From' phải trước hoặc bằng 'To'.");
    }
}

using FluentValidation;

namespace MusicLounge.Application.Analytics.Queries.GetTicketSalesTrend;

public sealed class GetTicketSalesTrendQueryValidator : AbstractValidator<GetTicketSalesTrendQuery>
{
    public GetTicketSalesTrendQueryValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0).WithMessage("ShowId không hợp lệ.");
    }
}

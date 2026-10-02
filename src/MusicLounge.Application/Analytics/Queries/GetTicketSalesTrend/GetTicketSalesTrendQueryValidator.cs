using FluentValidation;

namespace MusicLounge.Application.Analytics.Queries.GetTicketSalesTrend;

public sealed class GetTicketSalesTrendQueryValidator : AbstractValidator<GetTicketSalesTrendQuery>
{
    public GetTicketSalesTrendQueryValidator()
    {
        RuleFor(x => x.ShowId).NotEmpty().WithMessage("ShowId không hợp lệ.");
    }
}

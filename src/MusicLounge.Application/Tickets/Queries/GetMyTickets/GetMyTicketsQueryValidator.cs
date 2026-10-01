using FluentValidation;

namespace MusicLounge.Application.Tickets.Queries.GetMyTickets;

public sealed class GetMyTicketsQueryValidator : AbstractValidator<GetMyTicketsQuery>
{
    public GetMyTicketsQueryValidator()
    {
        // MLACP-499. Cùng trần với ô tìm kiếm buổi diễn (SearchLoungeShowsQueryValidator) — chỉ chặn chuỗi dài vô lý.
        RuleFor(x => x.Keyword)
            .MaximumLength(200)
            .When(x => x.Keyword is not null);
    }
}

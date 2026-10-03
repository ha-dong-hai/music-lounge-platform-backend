using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Queries.GetShowRatings;

// Page/PageSize khong validate loi ma clamp trong handler (cung quy uoc SearchLoungeShowsQueryValidator).
// Score thi KHAC: "?score=7" ma im lang tra danh sach rong se lam nguoi xem tuong chua ai cham 7 sao — noi ra thay vi doan.
public sealed class GetShowRatingsQueryValidator : AbstractValidator<GetShowRatingsQuery>
{
    public GetShowRatingsQueryValidator()
    {
        RuleFor(x => x.Score)
            .InclusiveBetween(1, 5).WithMessage("Số sao để lọc phải từ 1 đến 5.")
            .When(x => x.Score.HasValue);
    }
}

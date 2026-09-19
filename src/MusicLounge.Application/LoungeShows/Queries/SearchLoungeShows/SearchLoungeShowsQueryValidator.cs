using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;

// Page/PageSize khong validate loi ma clamp phong thu trong handler — 1 gia tri
// page/pageSize sai khong nen tra 400, chi can tu dong sua ve gia tri hop le.
public sealed class SearchLoungeShowsQueryValidator : AbstractValidator<SearchLoungeShowsQuery>
{
    public SearchLoungeShowsQueryValidator()
    {
        RuleFor(x => x.Keyword)
            .MaximumLength(200)
            .When(x => x.Keyword is not null);

        // MLACP-457. Khac page/pageSize o tren: gia la thu NGUOI DUNG GO, nen gia tri vo nghia phai duoc noi ra chu khong
        // im lang sua. Tra ve danh sach rong cho khoang 500.000–100.000 se lam nguoi dung tuong het buoi dien, trong khi
        // ho chi go nham hai o — im lang o day la noi doi.
        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Giá thấp nhất không được là số âm.")
            .Must(LaSoNguyenDong).WithMessage("Giá phải là số nguyên đồng (đồng Việt Nam không có đơn vị lẻ).")
            .When(x => x.MinPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Giá cao nhất không được là số âm.")
            .Must(LaSoNguyenDong).WithMessage("Giá phải là số nguyên đồng (đồng Việt Nam không có đơn vị lẻ).")
            .When(x => x.MaxPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(x => x.MinPrice)
            .WithMessage("Giá cao nhất phải lớn hơn hoặc bằng giá thấp nhất.")
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue);

        RuleFor(x => x.City)
            .MaximumLength(100)
            .When(x => x.City is not null);
    }

    /// <summary>VND khong co don vi le — toan bo he thong tinh tien bang so nguyen dong.</summary>
    private static bool LaSoNguyenDong(decimal? gia) => gia is null || gia.Value == decimal.Truncate(gia.Value);
}

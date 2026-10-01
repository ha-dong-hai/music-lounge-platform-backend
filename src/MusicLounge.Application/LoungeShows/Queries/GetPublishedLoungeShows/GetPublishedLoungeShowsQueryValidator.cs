using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Queries.GetPublishedLoungeShows;

public sealed class GetPublishedLoungeShowsQueryValidator : AbstractValidator<GetPublishedLoungeShowsQuery>
{
    public GetPublishedLoungeShowsQueryValidator()
    {
        // MLACP-498. Danh sách công khai (mine=false) không có hai bộ lọc này — lọc công khai nằm ở /search. Nói ra bằng
        // 400 thay vì lặng lẽ bỏ qua: bỏ qua thì người gọi nhận đủ mọi trạng thái/hình thức mà tưởng đã được lọc.
        RuleFor(x => x)
            .Must(x => x.Mine || (x.Status is null && x.Format is null))
            .WithMessage("Bộ lọc status và format chỉ dùng với mine=true; danh sách công khai hãy lọc qua /lounge-shows/search.");
    }
}

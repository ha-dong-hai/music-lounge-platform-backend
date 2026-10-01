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

        // MLACP-502 gộp với MLACP-498: keyword chỉ có ở danh sách công khai — đi cùng mine=true thì bị lặng lẽ bỏ qua và
        // người gọi tưởng đã được lọc. Nói ra bằng 400, cùng lý do với luật ở trên.
        RuleFor(x => x)
            .Must(x => !x.Mine || string.IsNullOrWhiteSpace(x.Keyword))
            .WithMessage("Từ khoá keyword chỉ dùng cho danh sách công khai (mine=false); buổi của tôi hãy lọc bằng status và format.");
    }
}

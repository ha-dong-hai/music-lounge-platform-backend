using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Moderations.Queries.GetPendingModerations;

public sealed class GetPendingModerationsQueryValidator : AbstractValidator<GetPendingModerationsQuery>
{
    public GetPendingModerationsQueryValidator()
    {
        // MLACP-504. Id chỉ có nghĩa trong một loại: buổi diễn 5 và livestream 5 là hai thứ khác nhau. targetId mà thiếu
        // (hoặc sai) targetType thì handler sẽ bỏ lọc loại và trả lẫn bản kiểm duyệt của loại khác — nói ra bằng 400.
        RuleFor(x => x.TargetType)
            .Must(t => Enum.TryParse<ModerationTargetType>(t, true, out _))
            .When(x => x.TargetId.HasValue)
            .WithMessage("Lọc theo targetId cần kèm targetType hợp lệ (Show, Livestream, GalleryImage, TourScene...).");
    }
}

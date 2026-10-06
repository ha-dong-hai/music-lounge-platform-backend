using FluentValidation;

namespace MusicLounge.Application.Moderations.Commands.ReviewLoungeMedia;

public sealed class ReviewLoungeMediaCommandValidator : AbstractValidator<ReviewLoungeMediaCommand>
{
    private static readonly string[] ValidDecisions = ["Approved", "Rejected"];
    private static readonly string[] ValidTargets = ["GalleryImage", "TourScene"];

    public ReviewLoungeMediaCommandValidator()
    {
        RuleFor(x => x.TargetId).NotEmpty().WithMessage("Mã nội dung không hợp lệ.");

        RuleFor(x => x.TargetType)
            .Must(t => ValidTargets.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Loại nội dung phải là 'GalleryImage' hoặc 'TourScene'.");

        RuleFor(x => x.Decision)
            .NotEmpty()
            .Must(d => ValidDecisions.Contains(d, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Quyết định phải là 'Approved' hoặc 'Rejected'.");

        RuleFor(x => x.ReviewNote)
            .MaximumLength(1000).WithMessage("Ghi chú duyệt không được vượt quá 1000 ký tự.");

        // Cùng quy ước MLACP-79 của duyệt buổi diễn / hạng vé: từ chối thì bắt buộc ghi lý do — chủ phòng trà đọc đúng câu
        // này để biết vì sao ảnh của mình bị gỡ.
        RuleFor(x => x.ReviewNote)
            .NotEmpty()
            .WithMessage("Phải ghi lý do khi từ chối.")
            .When(x => string.Equals(x.Decision, "Rejected", StringComparison.OrdinalIgnoreCase));
    }
}

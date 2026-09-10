using FluentValidation;

namespace MusicLounge.Application.Settlements.Commands.ReviewSettlement;

internal sealed class ReviewSettlementCommandValidator : AbstractValidator<ReviewSettlementCommand>
{
    public ReviewSettlementCommandValidator()
    {
        RuleFor(x => x.SettlementId).GreaterThan(0);

        RuleFor(x => x.Decision)
            .Must(d => d is "Release" or "Withhold")
            .WithMessage("Decision phải là 'Release' hoặc 'Withhold'.");

        // Bắt buộc có lý do: đây là quyết định về tiền của người khác, và nó đi vào thông báo gửi
        // cho chủ phòng trà — một quyết định giữ tiền không kèm lý do là thứ không giải thích được
        // với ai cả.
        RuleFor(x => x.Note)
            .NotEmpty().WithMessage("Phải ghi lý do cho quyết định này.")
            .MaximumLength(500);
    }
}

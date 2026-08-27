using FluentValidation;

namespace MusicLounge.Application.Livestreams.Commands.TerminateLivestream;

internal sealed class TerminateLivestreamCommandValidator : AbstractValidator<TerminateLivestreamCommand>
{
    public TerminateLivestreamCommandValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do buộc dừng livestream là bắt buộc.")
            .MaximumLength(1000).WithMessage("Lý do không vượt quá 1000 ký tự.");
    }
}

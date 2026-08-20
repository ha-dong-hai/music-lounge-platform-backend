using FluentValidation;

namespace MusicLounge.Application.Livestreams.Commands.SendChatMessage;

internal sealed class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
{
    public SendChatMessageCommandValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Tin nhắn không được để trống.")
            .MaximumLength(500).WithMessage("Tin nhắn không vượt quá 500 ký tự.");
    }
}

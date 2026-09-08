using FluentValidation;

namespace MusicLounge.Application.Livestreams.Commands.ProcessMuxWebhook;

public sealed class ProcessMuxWebhookCommandValidator : AbstractValidator<ProcessMuxWebhookCommand>
{
    public ProcessMuxWebhookCommandValidator()
    {
        RuleFor(x => x.RawBody).NotEmpty().WithMessage("RawBody không được rỗng.");
    }
}

using FluentValidation;

namespace MusicLounge.Application.Notifications.Commands.MarkNotificationRead;

public sealed class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(x => x.NotificationId).NotEmpty().WithMessage("NotificationId không hợp lệ.");
    }
}

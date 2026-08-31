using FluentValidation;

namespace MusicLounge.Application.Notifications.Commands.MarkNotificationRead;

public sealed class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(x => x.NotificationId).GreaterThan(0).WithMessage("NotificationId không hợp lệ.");
    }
}

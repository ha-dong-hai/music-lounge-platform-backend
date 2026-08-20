using FluentValidation;

namespace MusicLounge.Application.Admin.Commands.TriggerRecurringJob;

public sealed class TriggerRecurringJobCommandValidator : AbstractValidator<TriggerRecurringJobCommand>
{
    // Phai khop chinh xac cac recurring job id dang ky trong DependencyInjection.ConfigureRecurringJobs —
    // whitelist de tranh Admin vo tinh go sai id (Hangfire.TriggerJob se im lang no-op neu id sai).
    private static readonly string[] KnownJobIds =
    [
        "release-expired-holds",
        "refresh-recommendations",
        "auto-confirm-donations",
        "expire-stuck-donations",
        "cancel-abandoned-payments",
        "release-due-settlements",
        "send-event-reminders",
        "check-overdue-donations",
        "expire-ticket-transfers",
        "warn-expiring-subscriptions",
        "expire-subscriptions"
    ];

    public TriggerRecurringJobCommandValidator()
    {
        RuleFor(x => x.JobId)
            .Must(id => KnownJobIds.Contains(id))
            .WithMessage($"JobId phải là một trong: {string.Join(", ", KnownJobIds)}.");
    }
}

using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Auth.Jobs;

/// <summary>
/// Hangfire invokes this (not IEmailService directly) — same pattern as
/// SendPasswordResetEmailJob/SendEmailVerificationCodeJob, so a slow/unavailable SMTP provider
/// never delays the Register HTTP response the duplicate-email path is called from. Delaying via
/// background job also avoids a timing side-channel that an inline SendMailAsync call would create
/// between the "new email" and "duplicate email" register response paths.
/// </summary>
public sealed class SendDuplicateRegistrationAlertEmailJob
{
    private readonly IEmailService _emailService;

    public SendDuplicateRegistrationAlertEmailJob(IEmailService emailService)
        => _emailService = emailService;

    public Task ExecuteAsync(string toEmail, string toName, CancellationToken ct = default)
        => _emailService.SendDuplicateRegistrationAlertEmailAsync(toEmail, toName, ct);
}

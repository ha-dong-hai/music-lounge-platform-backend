using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Auth.Jobs;

/// <summary>
/// Hangfire invokes this (not IEmailService directly) so the raw OTP code only ever exists in
/// plaintext in memory, never inside Hangfire's own persistent job storage — see ISecretProtector.
/// </summary>
public sealed class SendEmailVerificationCodeJob
{
    private readonly IEmailService _emailService;
    private readonly ISecretProtector _secretProtector;

    public SendEmailVerificationCodeJob(IEmailService emailService, ISecretProtector secretProtector)
    {
        _emailService = emailService;
        _secretProtector = secretProtector;
    }

    public Task ExecuteAsync(
        string toEmail, string toName, string protectedCode, CancellationToken ct = default)
        => _emailService.SendEmailVerificationCodeAsync(
            toEmail, toName, _secretProtector.Unprotect(protectedCode), ct);
}

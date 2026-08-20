using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Auth.Jobs;

/// <summary>
/// Hangfire invokes this (not IEmailService directly) so the raw reset link only ever exists in
/// plaintext in memory, never inside Hangfire's own persistent job storage — see ISecretProtector.
/// </summary>
public sealed class SendPasswordResetEmailJob
{
    private readonly IEmailService _emailService;
    private readonly ISecretProtector _secretProtector;

    public SendPasswordResetEmailJob(IEmailService emailService, ISecretProtector secretProtector)
    {
        _emailService = emailService;
        _secretProtector = secretProtector;
    }

    public Task ExecuteAsync(
        string toEmail, string toName, string protectedResetLink, CancellationToken ct = default)
        => _emailService.SendPasswordResetEmailAsync(
            toEmail, toName, _secretProtector.Unprotect(protectedResetLink), ct);
}

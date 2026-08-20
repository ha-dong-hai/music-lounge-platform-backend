using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Auth.Jobs;

/// <summary>
/// Hangfire invokes this (not ISmsService directly) so the raw OTP code only ever exists in
/// plaintext in memory, never inside Hangfire's own persistent job storage — mirrors
/// SendEmailVerificationCodeJob exactly (see ISecretProtector).
/// </summary>
public sealed class SendPhoneVerificationCodeJob
{
    private readonly ISmsService _smsService;
    private readonly ISecretProtector _secretProtector;

    public SendPhoneVerificationCodeJob(ISmsService smsService, ISecretProtector secretProtector)
    {
        _smsService = smsService;
        _secretProtector = secretProtector;
    }

    public Task ExecuteAsync(string toPhone, string protectedCode, CancellationToken ct = default)
        => _smsService.SendPhoneVerificationCodeAsync(toPhone, _secretProtector.Unprotect(protectedCode), ct);
}

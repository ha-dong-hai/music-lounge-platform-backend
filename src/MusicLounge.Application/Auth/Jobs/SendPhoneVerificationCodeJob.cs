using MusicLounge.Domain.ValueObjects;
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

    public Task ExecuteAsync(string toPhone, string protectedCode, string language, CancellationToken ct = default)
        => _smsService.SendPhoneVerificationCodeAsync(
            toPhone, _secretProtector.Unprotect(protectedCode), language, ct);

    /// <summary>MLACP-489: chữ ký cũ, giữ cho job đã xếp hàng trước lần deploy có ngôn ngữ — xoá sau một lần deploy.</summary>
    public Task ExecuteAsync(string toPhone, string protectedCode, CancellationToken ct = default)
        => ExecuteAsync(toPhone, protectedCode, NgonNgu.Viet, ct);
}

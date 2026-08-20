using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Services;

// Stub — no SMS gateway integrated yet (same status as FcmService: "chưa tích hợp thật" per
// docs/09-tech-knowledge.md). Logs instead of sending so the phone-verification flow (fields,
// hashing, rate-limit, expiry) is fully wired end-to-end and only needs a real provider (e.g. an
// SMS gateway with Vietnamese carrier support) plugged in behind this interface later — closes
// governance gap #4 (phone_verified was a dead field with no verification flow at all) without
// fabricating a fake third-party integration.
internal sealed class SmsService : ISmsService
{
    private readonly ILogger<SmsService> _logger;

    public SmsService(ILogger<SmsService> logger) => _logger = logger;

    public Task SendPhoneVerificationCodeAsync(string toPhone, string code, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "SMS NOT SENT (no SMS gateway configured) — phone={Phone} verificationCode={Code}",
            toPhone, code);
        return Task.CompletedTask;
    }
}

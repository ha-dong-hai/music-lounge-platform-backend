namespace MusicLounge.Application.Common.Interfaces;

public interface ISmsService
{
    Task SendPhoneVerificationCodeAsync(string toPhone, string code, CancellationToken ct = default);
}

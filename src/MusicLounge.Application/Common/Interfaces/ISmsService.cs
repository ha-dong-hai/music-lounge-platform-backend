namespace MusicLounge.Application.Common.Interfaces;

public interface ISmsService
{
    // MLACP-489: language = User.PreferredLanguage của người nhận ("vi" | "en").
    Task SendPhoneVerificationCodeAsync(string toPhone, string code, string language, CancellationToken ct = default);
}

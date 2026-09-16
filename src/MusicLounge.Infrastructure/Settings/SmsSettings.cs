namespace MusicLounge.Infrastructure.Settings;

/// <summary>
/// MLACP-426: Twilio Programmable Messaging. De trong thi khong gui that — ghi log va khong nem loi, cung nep
/// "thieu cau hinh thi suy bien" voi Email/Firebase — va bang kiem cau hinh (MLACP-420) bao thieu.
/// </summary>
public sealed class SmsSettings
{
    public string AccountSid { get; init; } = string.Empty;
    public string AuthToken { get; init; } = string.Empty;

    /// <summary>So gui di cua Twilio, dang E.164 (vd +15551234567).</summary>
    public string FromNumber { get; init; } = string.Empty;
}

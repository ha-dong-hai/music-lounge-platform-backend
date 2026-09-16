namespace MusicLounge.Application.Common.Configuration;

/// <summary>Mot cai dat con thieu va HAU QUA that cua no — khong bao gio chua gia tri cai dat.</summary>
/// <param name="Feature">Tinh nang bi anh huong, goi ten theo nghiep vu.</param>
/// <param name="Key">Ten cai dat can dien (vd "Mux:WebhookSecret").</param>
/// <param name="Impact">Chuyen gi dang xay ra voi nguoi dung khi thieu cai dat nay.</param>
/// <param name="Severity">Broken = tinh nang khong dung duoc; Degraded = van chay nhung mat mot lop.</param>
public sealed record ConfigurationGap(string Feature, string Key, string Impact, ConfigurationGapSeverity Severity);

public enum ConfigurationGapSeverity
{
    Degraded,
    Broken
}

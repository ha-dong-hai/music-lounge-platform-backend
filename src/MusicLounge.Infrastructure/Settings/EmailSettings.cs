namespace MusicLounge.Infrastructure.Settings;

public sealed class EmailSettings
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool EnableSsl { get; init; } = true;
    public string FromAddress { get; init; } = "no-reply@musiclounge.local";
    public string FromName { get; init; } = "MusicLounge";
}

namespace MusicLounge.Application.Common;

public class EmailSettings
{
    public const string SectionName = "MailSettings";

    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
}

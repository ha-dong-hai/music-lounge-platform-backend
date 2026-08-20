namespace MusicLounge.Application.Common.Interfaces;

public interface IFcmService
{
    Task SendAsync(int userId, string title, string body, CancellationToken ct = default);
    Task SendAsync(int userId, string title, string body,
        Dictionary<string, string> data, CancellationToken ct = default);
}

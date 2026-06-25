// CoreFlow: All — wraps DateTime.UtcNow so tests can control the current time.
// Handlers must use this instead of DateTime.UtcNow directly to remain deterministic in tests.
namespace MusicLounge.Application.Common.Interfaces;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

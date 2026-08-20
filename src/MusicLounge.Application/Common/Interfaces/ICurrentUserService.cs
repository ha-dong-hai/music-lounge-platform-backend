namespace MusicLounge.Application.Common.Interfaces;

public interface ICurrentUserService
{
    int UserId { get; }
    string Role { get; }
    int? LoungeId { get; }
    bool IsAuthenticated { get; }
}

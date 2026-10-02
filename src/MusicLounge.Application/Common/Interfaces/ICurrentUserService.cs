namespace MusicLounge.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid UserId { get; }
    string Role { get; }
    Guid? LoungeId { get; }
    bool IsAuthenticated { get; }
    Guid SecurityStamp { get; }
}

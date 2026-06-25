// CoreFlow: All — provides the identity of the authenticated user making the current request.
// Populated from the JWT token by the API layer via HttpContext.
// Application layer uses this to enforce ownership and role checks without touching HTTP.
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common.Interfaces;

public interface ICurrentUser
{
    int Id { get; }
    string Email { get; }
    UserRole Role { get; }

    // False when the endpoint allows anonymous access — always check before reading Id
    bool IsAuthenticated { get; }
}

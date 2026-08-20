using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Commands.RequestDataErasure;

// CurrentPassword is required only when the account has a local password (AuthProvider="local")
// — Google-only accounts have no password to confirm, and an authenticated session is treated as
// sufficient proof of control for those, matching how OAuth-only account-deletion flows are
// commonly handled elsewhere (there's nothing else to check).
public sealed record RequestDataErasureCommand(string? CurrentPassword) : ICommand;

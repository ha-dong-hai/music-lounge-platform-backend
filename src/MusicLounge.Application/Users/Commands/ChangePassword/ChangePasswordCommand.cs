using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Commands.ChangePassword;

// The only existing way to set a new password while already logged in was the forgot-password OTP
// flow (POST /auth/forgot-password + /auth/reset-password) — works, but forces an email round-trip
// even when the user already knows their current password and just wants to change it from
// settings. This is the direct self-service counterpart.
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand;

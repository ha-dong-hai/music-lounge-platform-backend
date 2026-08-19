using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Commands.RequestChangeEmail;

// Step 1 of 2 — sends an OTP to NewEmail; nothing is persisted onto User.Email until
// ConfirmChangeEmailCommand verifies it. There was previously no endpoint at all to change the
// email set at Register.
public sealed record RequestChangeEmailCommand(string NewEmail) : ICommand;

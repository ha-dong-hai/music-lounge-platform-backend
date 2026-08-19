using MediatR;

namespace MusicLounge.Application.Users.Commands.ConfirmChangeEmail;

// Step 2 of 2 — verifies the OTP RequestChangeEmailCommand sent to the pending new address, then
// actually moves it onto User.Email.
public sealed record ConfirmChangeEmailCommand(string Code) : IRequest<Unit>;

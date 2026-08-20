using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand;

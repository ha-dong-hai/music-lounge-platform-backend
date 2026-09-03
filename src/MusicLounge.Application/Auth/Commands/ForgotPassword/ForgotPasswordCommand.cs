using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Auth.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : ICommand;

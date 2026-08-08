using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Auth.Commands.ResendVerificationCode;

public sealed record ResendVerificationCodeCommand(string Email) : ICommand;

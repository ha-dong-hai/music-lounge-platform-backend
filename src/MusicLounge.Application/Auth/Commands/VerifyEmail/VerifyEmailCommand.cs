using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Auth.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(string Email, string Code) : ICommand<AuthResultDto>, INoTransactionCommand;

using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Auth.Commands.Login;

public sealed record LoginCommand(
    string Email,
    string Password,
    string? IpAddress = null
) : ICommand<AuthResultDto>, INoTransactionCommand;

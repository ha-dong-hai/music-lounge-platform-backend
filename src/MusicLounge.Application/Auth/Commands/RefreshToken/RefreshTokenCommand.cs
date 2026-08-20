using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AuthResultDto>;

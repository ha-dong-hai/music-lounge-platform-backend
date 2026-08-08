using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Auth.Commands.GoogleLogin;

public sealed record GoogleLoginCommand(
    string IdToken
) : ICommand<AuthResultDto>;

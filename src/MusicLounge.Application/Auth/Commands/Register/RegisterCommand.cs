using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Auth.Commands.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FullName,
    string? Phone,
    bool AcceptTerms,
    string Role = "Audience"
) : ICommand<RegisterResultDto>;

using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Commands.UpdateMyProfile;

public sealed record UpdateMyProfileCommand(
    string FullName,
    string? Phone,
    string? AvatarUrl) : ICommand;

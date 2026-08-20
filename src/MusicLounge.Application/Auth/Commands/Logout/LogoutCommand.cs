using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Auth.Commands.Logout;

/// <summary>No input — always logs out the current authenticated user (ICurrentUserService).</summary>
public sealed record LogoutCommand : ICommand;

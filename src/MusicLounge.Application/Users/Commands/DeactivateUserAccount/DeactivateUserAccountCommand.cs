using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Commands.DeactivateUserAccount;

public sealed record DeactivateUserAccountCommand(int UserId) : ICommand;

using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Commands.DeactivateUserAccount;

public sealed record DeactivateUserAccountCommand(Guid UserId) : ICommand;

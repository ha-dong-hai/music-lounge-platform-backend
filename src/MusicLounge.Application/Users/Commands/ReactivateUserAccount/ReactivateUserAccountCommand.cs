using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Commands.ReactivateUserAccount;

public sealed record ReactivateUserAccountCommand(Guid UserId) : ICommand;

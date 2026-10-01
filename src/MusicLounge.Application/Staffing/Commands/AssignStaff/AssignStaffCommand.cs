using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Staffing.Commands.AssignStaff;

public sealed record AssignStaffCommand(Guid LoungeId, Guid UserId) : ICommand<Guid>;

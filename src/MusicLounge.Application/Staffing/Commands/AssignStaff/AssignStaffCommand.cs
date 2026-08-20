using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Staffing.Commands.AssignStaff;

public sealed record AssignStaffCommand(int LoungeId, int UserId) : ICommand<int>;

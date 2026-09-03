using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Staffing.Commands.DeactivateStaff;

public sealed record DeactivateStaffCommand(int LoungeStaffId) : ICommand;

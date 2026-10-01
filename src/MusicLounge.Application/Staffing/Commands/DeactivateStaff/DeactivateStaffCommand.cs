using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Staffing.Commands.DeactivateStaff;

public sealed record DeactivateStaffCommand(Guid LoungeStaffId) : ICommand;

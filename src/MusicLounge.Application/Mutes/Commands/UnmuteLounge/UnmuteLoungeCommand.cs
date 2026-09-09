using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Mutes.Commands.UnmuteLounge;

/// <summary>Bỏ tắt tiếng. Phải có, nếu không thì một lần bấm nhầm là vĩnh viễn.</summary>
public sealed record UnmuteLoungeCommand(int LoungeId) : ICommand;

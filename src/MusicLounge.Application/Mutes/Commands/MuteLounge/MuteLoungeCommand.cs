using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Mutes.Commands.MuteLounge;

/// <summary>"Đừng gợi ý phòng trà này cho tôi nữa."</summary>
public sealed record MuteLoungeCommand(int LoungeId) : ICommand;

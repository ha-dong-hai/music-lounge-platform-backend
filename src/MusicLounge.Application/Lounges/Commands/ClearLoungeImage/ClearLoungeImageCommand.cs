using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.ClearLoungeImage;

/// <summary>MLACP-506. Gỡ ảnh đại diện của phòng trà (để trống).</summary>
public sealed record ClearLoungeImageCommand(Guid LoungeId) : ICommand;

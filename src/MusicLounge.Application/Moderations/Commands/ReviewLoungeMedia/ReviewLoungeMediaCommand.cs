using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Moderations.Commands.ReviewLoungeMedia;

/// <summary>
/// MLACP-692: Admin duyệt ảnh thư viện / cảnh tour 360 bị AI gắn cờ "cần xem" (ImageModerationGate, ngưỡng review).
/// <paramref name="TargetType"/> là "GalleryImage" hoặc "TourScene".
/// </summary>
public sealed record ReviewLoungeMediaCommand(
    string TargetType,
    Guid TargetId,
    string Decision,
    string? ReviewNote
) : ICommand;

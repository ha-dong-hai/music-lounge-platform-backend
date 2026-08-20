namespace MusicLounge.Application.Livestreams.DTOs;

/// <summary>Nhạy cảm — chỉ trả về cho Staff/Admin của venue, không bao giờ lộ ra viewer.</summary>
public sealed record LivestreamCredentialsDto(
    int Id,
    string? Provider,
    string? RtmpUrl,
    string? StreamKey);

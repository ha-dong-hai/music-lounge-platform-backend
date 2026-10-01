using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.UpdateLounge;

public sealed record UpdateLoungeCommand(
    int LoungeId,
    string Name,
    string? Description,
    int? AtmosphereId,
    string Street,
    string? Ward,
    string? District,
    string? City,
    double? Latitude,
    double? Longitude,
    // MLACP-521: tuỳ chọn — mã tỉnh/xã theo QĐ 19/2025/QĐ-TTg (xem ILoungeAddressInput).
    string? ProvinceCode = null,
    string? WardCode = null
) : ICommand, ILoungeAddressInput;

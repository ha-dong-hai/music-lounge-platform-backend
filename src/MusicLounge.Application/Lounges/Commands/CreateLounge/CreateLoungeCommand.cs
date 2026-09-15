using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.CreateLounge;

/// <param name="District">
/// MLACP-403. Địa chỉ theo đơn vị hành chính mới (từ 01/7/2025) không còn cấp quận: số nhà, đường, phường, thành phố. Bỏ trống
/// thì lưu rỗng. Trước đây trường này không nullable nên ASP.NET tự coi là bắt buộc và trả 400, dù validator cho bỏ trống.
/// </param>
public sealed record CreateLoungeCommand(
    string Name,
    string? Description,
    int? AtmosphereId,
    string Street,
    string Ward,
    string? District,
    string City,
    double? Latitude,
    double? Longitude
) : ICommand<int>;

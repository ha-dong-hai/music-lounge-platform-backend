using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record PerformerSummaryDto(
    int Id,
    string Name,
    string? AvatarUrl,
    string? Bio,
    IReadOnlyList<GenreDto> Genres,
    int PerformanceId,
    bool AcceptsDonation,
    PerformerRole Role,
    // MLACP-469: UpdatePerformanceCommand BẮT BUỘC gửi OrderIndex, mà trước đây không DTO đọc nào
    // trả về. Giao diện phải suy từ vị trí trong mảng — hai không gian khác nhau: số đang lưu có
    // thể là 0, 5, 10, nên sửa vai trò của người thứ ba mà gửi 2 là đẩy họ lên trước người thứ hai
    // dù không ai đổi thứ tự. Lỗi này đã xảy ra thật trên giao diện.
    int OrderIndex,
    TimeOnly? SetTime);

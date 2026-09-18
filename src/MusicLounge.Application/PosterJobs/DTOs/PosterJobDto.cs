namespace MusicLounge.Application.PosterJobs.DTOs;

/// <summary>
/// MLACP-458. Một đơn poster giao cho máy trạm.
///
/// Cố ý CHỈ có lời nhắc và khổ ảnh: máy trạm nằm ngoài hệ thống (máy cá nhân của người vận hành, gọi Google Flow bằng tài
/// khoản cá nhân), nên nó không được nhận tên chủ phòng trà, email, hay bất cứ dữ liệu người dùng nào. Lời nhắc đã chứa tên
/// buổi hòa nhạc và tên phòng trà — đó là thông tin công khai, in trên chính poster.
/// </summary>
public sealed record PosterJobDto(
    int Id,
    int ShowId,
    string Prompt,
    string AspectRatio,
    int AttemptCount);

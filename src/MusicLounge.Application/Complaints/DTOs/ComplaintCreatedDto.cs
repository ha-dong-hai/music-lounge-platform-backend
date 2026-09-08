namespace MusicLounge.Application.Complaints.DTOs;

/// <param name="LookupReference">
/// Chỉ có với khiếu nại của người KHÔNG đăng nhập. Giao diện phải hiển thị mã này ngay và nhắc lưu
/// lại — đây là cách duy nhất họ tra được kết quả về sau. Null với người đã có tài khoản, vì họ xem
/// được qua GET /complaints/my.
/// </param>
public sealed record ComplaintCreatedDto(int Id, string? LookupReference);

namespace MusicLounge.Application.Complaints.DTOs;

/// <param name="LookupReference">
/// MLACP-690: có với MỌI khiếu nại (trước đây chỉ khách không đăng nhập). Giao diện hiển thị ngay; với khách vãng lai đây
/// là cách duy nhất tra được kết quả về sau, với người có tài khoản thì mã cũng hiện lại trong GET /complaints/my.
/// </param>
public sealed record ComplaintCreatedDto(Guid Id, string? LookupReference);

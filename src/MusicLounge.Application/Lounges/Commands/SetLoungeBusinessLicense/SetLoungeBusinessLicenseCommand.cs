using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeBusinessLicense;

/// <param name="DocumentUrl">
/// URL trả về từ POST /uploads/images. File được chuyển sang vùng lưu riêng tư ngay khi được nhận
/// ở đây, nên URL này chỉ dùng được một lần.
/// </param>
public sealed record SetLoungeBusinessLicenseCommand(int LoungeId, string DocumentUrl) : ICommand;

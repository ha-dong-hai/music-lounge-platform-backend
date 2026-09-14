using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Commands.SubmitCitizenCard;

/// <param name="DateOfBirth">
/// MLACP-397. Ngày sinh như trên CCCD/CMND — cùng họ tên và số giấy tờ là thông tin xác thực người bán là cá nhân
/// (NĐ 248/2026/NĐ-CP Điều 18). Thiếu nó thì Admin không có gì để đối chiếu với ảnh giấy tờ. Để nullable để bỏ trống
/// thì validator trả 400 có câu rõ ràng, thay vì lỗi đọc JSON.
/// </param>
public sealed record SubmitCitizenCardCommand(
    string CitizenCardNumber,
    string FrontImageUrl,
    string BackImageUrl,
    DateOnly? DateOfBirth) : ICommand;

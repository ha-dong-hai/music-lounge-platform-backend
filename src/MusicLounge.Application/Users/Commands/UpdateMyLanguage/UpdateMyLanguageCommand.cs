using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Commands.UpdateMyLanguage;

/// <summary>
/// MLACP-489. Đổi ngôn ngữ nhận push, email và SMS. Lệnh riêng, không gộp vào <c>PUT /me/profile</c>: lệnh đó ghi đè
/// toàn phần (FullName, Phone, AvatarUrl), gộp vào thì client chỉ muốn đổi ngôn ngữ cũng phải gửi lại cả hồ sơ —
/// đúng kiểu "ghi mà không đọc" đã làm mất dữ liệu bốn lần ở repo này.
/// </summary>
/// <param name="PreferredLanguage"><c>"vi"</c> hoặc <c>"en"</c>. Cùng tên với trường đọc ở <c>UserProfileDto</c> — hai
/// đầu khác tên thì frontend phải tự ánh xạ, và đó là chỗ test EditableFieldsAreReadable được viết ra để chặn.</param>
public sealed record UpdateMyLanguageCommand(string PreferredLanguage) : ICommand;

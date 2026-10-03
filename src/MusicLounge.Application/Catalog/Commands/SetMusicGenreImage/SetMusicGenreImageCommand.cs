using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.SetMusicGenreImage;

/// <summary>
/// MLACP-581: Admin đặt (hoặc gỡ, khi <c>ImageUrl</c> = null) ảnh riêng cho một thể loại nhạc. Ảnh này hiện trên thẻ
/// thể loại ở trang chủ; thể loại chưa có ảnh thì giao diện vẫn mượn ảnh của một buổi hòa nhạc thuộc thể loại đó.
///
/// Tách khỏi <c>PUT /admin/genres/{id}</c> có chủ đích: lệnh sửa tên ghi đè mọi trường nó nhận (đã từng xoá mất NameEn,
/// xem chú thích ở AdminController). Gộp ảnh vào đó thì mỗi lần sửa tên mà quên gửi ảnh là mất ảnh.
/// </summary>
public sealed record SetMusicGenreImageCommand(Guid Id, string? ImageUrl) : ICommand;

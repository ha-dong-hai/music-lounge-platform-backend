namespace MusicLounge.Application.Catalog.DTOs;

// MLACP-581: danh mục thể loại công khai kèm ảnh riêng (null = Admin chưa đặt; giao diện tự mượn ảnh của một buổi hòa
// nhạc thuộc thể loại đó). Không thêm ImageUrl vào CatalogItemDto vì record đó dùng chung cho tâm trạng / không gian /
// loại buổi — những thứ không có ảnh.
public sealed record MusicGenreCatalogItemDto(Guid Id, string Name, string? ImageUrl);

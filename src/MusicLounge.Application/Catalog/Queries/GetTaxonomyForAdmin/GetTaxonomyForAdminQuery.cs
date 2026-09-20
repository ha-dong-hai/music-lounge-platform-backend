using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Queries.GetTaxonomyForAdmin;

/// <summary>
/// Danh mục phân loại nhìn từ phía Admin: ĐẦY ĐỦ các trường mà lệnh sửa ghi đè, và không lọc bỏ mục đã tắt.
///
/// <para>Đường đọc công khai (<c>GET /catalog/event-categories</c>) cố ý chỉ trả <c>(Id, Name)</c> và chỉ
/// trả mục đang bật — đúng cho khán giả. Nhưng đó cũng là đường đọc DUY NHẤT, trong khi
/// <c>PUT /admin/event-categories/{id}</c> ghi đè toàn phần cả <c>Description</c> lẫn <c>IsActive</c>.
/// Hậu quả:</para>
/// <list type="bullet">
/// <item>Sửa tên một danh mục là xoá luôn mô tả, vì màn hình không đọc được mô tả để gửi lại.</item>
/// <item>Tắt một danh mục xong thì KHÔNG màn hình nào còn nhìn thấy nó nữa để bật lại — cửa một chiều,
/// chỉ mở lại được bằng cách sửa tay trong cơ sở dữ liệu.</item>
/// </list>
///
/// <para>Thể loại nhạc cùng một lớp lỗi: <c>PUT /admin/genres/{id}</c> nhận <c>NameEn</c> mà không đường
/// đọc nào trả về, nên sửa tên tiếng Việt là mất tên tiếng Anh.</para>
///
/// <para>Tâm trạng và không khí không có vấn đề này: lệnh sửa của chúng chỉ nhận <c>Name</c>, mà
/// <c>Name</c> thì đọc được từ danh mục công khai.</para>
/// </summary>
public sealed record GetEventCategoriesForAdminQuery : IQuery<List<AdminEventCategoryDto>>;

/// <inheritdoc cref="GetEventCategoriesForAdminQuery"/>
public sealed record GetMusicGenresForAdminQuery : IQuery<List<AdminMusicGenreDto>>;

public sealed record AdminEventCategoryDto(int Id, string Name, string? Description, bool IsActive);

public sealed record AdminMusicGenreDto(int Id, string Name, string? NameEn);

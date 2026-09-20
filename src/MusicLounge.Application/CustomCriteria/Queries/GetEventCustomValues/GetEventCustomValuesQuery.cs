using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.CustomCriteria.Queries.GetEventCustomValues;

/// <summary>
/// Giá trị tiêu chí riêng đã gắn cho một buổi hòa nhạc.
///
/// <para><c>POST /custom-criteria/shows/{showId}/values</c> là THAY THẾ TOÀN BỘ: gửi lên danh sách nào thì
/// buổi diễn còn đúng danh sách đó. Nhưng trước task này không có đường nào đọc giá trị đang gắn, nên màn
/// hình sửa không thể hiện được cái gì đang có, và mỗi lần lưu là chủ phòng trà phải nhập lại từ đầu — quên
/// một tiêu chí là tiêu chí đó biến mất.</para>
///
/// <para>Trả kèm định nghĩa của tiêu chí (tên, kiểu dữ liệu, danh sách lựa chọn) để màn hình dựng được ô
/// nhập đúng kiểu mà không phải gọi thêm một lượt <c>GET /custom-criteria?loungeId=</c> rồi tự ghép.</para>
/// </summary>
public sealed record GetEventCustomValuesQuery(int ShowId)
    : IQuery<IReadOnlyList<EventCustomValueDto>>;

/// <param name="Value">Giá trị đang lưu, dạng JSON đúng như lệnh ghi nhận vào.</param>
/// <param name="CriteriaIsActive">
/// Tiêu chí đã bị tắt nhưng giá trị cũ vẫn còn gắn ở đây. Màn hình cần biết để hiển thị mờ thay vì lặng lẽ
/// bỏ qua — bỏ qua rồi lưu lại là xoá mất giá trị cũ.
/// </param>
public sealed record EventCustomValueDto(
    int CriteriaId,
    string Name,
    string Key,
    CustomCriteriaDataType DataType,
    string? Options,
    bool CriteriaIsActive,
    string Value);

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
/// <param name="ValidationError">
/// Lý do giá trị ĐANG LƯU không khớp kiểu dữ liệu của tiêu chí; <c>null</c> nghĩa là hợp lệ.
///
/// <para>Tính bằng ĐÚNG hàm mà lệnh ghi dùng để từ chối (<see cref="CustomCriteriaValue.LoiNeuCo"/>), nên
/// không thể có chuyện màn hình báo hợp lệ mà lưu lại bị từ chối, hay ngược lại.</para>
///
/// <para>Vì sao cần: giá trị sai vẫn còn trong cơ sở dữ liệu từ trước khi MLACP-470 dựng hàng rào, và ô
/// chọn không khớp lựa chọn nào thì trình duyệt hiện Ô TRỐNG — trông y hệt "chưa đặt". Vì lệnh ghi là
/// THAY THẾ TOÀN BỘ, lần Lưu kế tiếp xoá luôn giá trị đó mà không ai thấy. Có trường này thì mỗi màn
/// hình chỉ việc hiển thị, không phải bên nào cũng tự chép lại luật so khớp — hiện đã có hai bản (C# và
/// JavaScript) và ứng dụng nhân viên sẽ là bản thứ ba. Hai bản đầu đã từng lệch nhau ở hai ca.</para>
///
/// <para>ĐỪNG DỌN ĐI VÌ THỬ MÃI KHÔNG THẤY NÓ CHẠY. Thử trên hệ thống đang chạy sẽ không bao giờ thấy
/// trường này khác <c>null</c>, và đó là ĐÚNG: sau MLACP-470 và MLACP-472 thì không còn đường API nào
/// ghi được một giá trị sai vào cơ sở dữ liệu nữa (đã thử thật trên Azure ngày 21/09/2026 — gửi một giá
/// trị ngoài danh sách thì bị từ chối 422). Nó chỉ khác <c>null</c> với dòng ghi TRƯỚC khi có hai hàng
/// rào đó. Xoá đi thì một dòng dữ liệu cũ đi qua sẽ mất giá trị IM LẶNG đúng lúc có người bấm Lưu — đúng
/// lớp lỗi mà chính trường này sinh ra để chặn. <c>EventCustomValueValidationReasonTests</c> giữ hành vi
/// này, xoá là đỏ.</para>
/// </param>
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
    string Value,
    string? ValidationError);

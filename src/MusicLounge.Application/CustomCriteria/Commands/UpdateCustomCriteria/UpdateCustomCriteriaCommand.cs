using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.CustomCriteria.Commands.UpdateCustomCriteria;

/// <summary>
/// Sửa tên hiển thị của một tiêu chí riêng, và bật/tắt việc dùng nó.
///
/// <para>Trước task này tiêu chí tạo xong là VĨNH VIỄN: không có đường nào sửa, xoá, hay ngừng dùng. Chủ
/// phòng trà gõ sai tên một tiêu chí thì cái tên sai đó ở lại trên màn hình sửa của MỌI buổi diễn, không
/// cách nào chữa trong sản phẩm. Trường <c>IsActive</c> có sẵn trong bảng nhưng không dòng mã nào từng
/// đặt nó thành false, nên bộ lọc "chỉ lấy tiêu chí đang dùng" không bao giờ loại được gì.</para>
///
/// <para>CỐ Ý KHÔNG cho sửa <c>Key</c>, <c>DataType</c> và <c>Options</c>:</para>
/// <list type="bullet">
/// <item><c>Key</c> là mã định danh máy đọc, duy nhất trong phòng trà; đổi nó là đổi thứ bên ngoài đang
/// tham chiếu tới.</item>
/// <item><c>DataType</c> đổi thì MỌI giá trị đã gắn cho các buổi diễn lập tức sai kiểu — không phải sửa
/// tiêu chí nữa mà là làm hỏng dữ liệu cũ.</item>
/// <item><c>Options</c> thì hẹp hơn một chút: THÊM lựa chọn là an toàn, BỚT lựa chọn làm các giá trị đang
/// gắn thành lạc. Cho sửa được đúng cách thì phải kiểm lại toàn bộ giá trị đang gắn của tiêu chí đó, và
/// phải kiểm hình dạng danh sách mới — phần đó nằm ở MLACP-472, chưa vào master lúc viết task này. Đường
/// nâng cấp: sau khi 472 vào, thêm trường Options vào lệnh này và từ chối nếu có giá trị nào đang gắn
/// không còn hợp lệ với danh sách mới.</item>
/// </list>
///
/// <para>Không có lệnh XOÁ. Tiêu chí đã bị xoá hẳn sẽ bỏ lại giá trị mồ côi ở các buổi diễn và ở sở thích
/// người dùng; tắt đi giữ nguyên lịch sử mà vẫn đạt mục đích "không dùng nữa".</para>
/// </summary>
public sealed record UpdateCustomCriteriaCommand(int Id, string Name, bool IsActive) : ICommand;

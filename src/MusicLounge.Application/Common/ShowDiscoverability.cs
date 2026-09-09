using System.Linq.Expressions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// Buổi diễn nào được phép xuất hiện trước mặt người đang tìm chỗ đi nghe nhạc.
///
/// Viết một lần ở đây rồi dùng cho cả truy vấn xuống database lẫn phần lọc trong bộ nhớ. Chúng là <see cref="Expression"/> chứ không phải hàm thường vì phía repository cần dịch
/// được sang SQL; phía trong bộ nhớ dùng lại đúng biểu thức đó qua
/// <c>AsQueryable().Where(...)</c>, nên không có hai bản cài đặt để lệch nhau — đúng lớp lỗi
/// MLACP-322 đã trả giá.
/// </summary>
public static class ShowDiscoverability
{
    /// <summary>
    /// Phòng trà còn đang được phép hoạt động.
    ///
    /// <b>MLACP-326.</b> Trước đây không truy vấn buổi diễn nào lọc theo trạng thái phòng trà. Cổng
    /// duyệt BR-01 (MLACP-307) chỉ chặn lúc ĐĂNG, không thu hồi những gì đã đăng — nên khi Admin
    /// đình chỉ một phòng trà, hệ thống vẫn tiếp tục chủ động đem buổi diễn của họ đi mời người
    /// dùng mua vé. Đình chỉ mà vẫn quảng bá thì việc đình chỉ chẳng có nghĩa gì.
    ///
    /// <b>Cảnh cáo thì không nằm trong nhóm bị chặn.</b> <see cref="LoungeStatus.Warned"/> là một
    /// lời nhắc nhở, không phải một lệnh dừng: phòng trà đó vẫn đang hoạt động và vẫn bán vé. Cắt
    /// họ khỏi màn hình khám phá là tự ý nâng một lời cảnh cáo thành một hình phạt kinh tế mà không
    /// ai quyết định điều đó.
    ///
    /// <b>Còn thiếu ở nơi khác.</b> Cùng thiếu sót này vẫn còn ở đường duyệt và tìm kiếm
    /// (<c>GetPublishedAsync</c>, <c>SearchAsync</c>) — chúng cũng không lọc trạng thái phòng trà.
    /// Task này chỉ đụng đường của hệ gợi ý; hai chỗ kia cần task riêng vì nằm ngoài phạm vi AI.
    /// Khi làm, chỉ cần thêm đúng biểu thức này vào là xong.
    /// </summary>
    public static Expression<Func<LoungeShow, bool>> VenueIsOperating
        => s => s.Lounge.Status == LoungeStatus.Approved
             || s.Lounge.Status == LoungeStatus.Warned;

    /// <summary>
    /// Buổi diễn nằm trong thành phố người dùng đang lọc.
    ///
    /// <b>Một câu hỏi còn để ngỏ, cố ý chưa tự quyết.</b> Quy tắc hiện tại so thẳng địa chỉ phòng
    /// trà, nên nó loại luôn cả buổi diễn TRỰC TUYẾN của phòng trà ở tỉnh khác — trong khi nền tảng
    /// có hẳn loại vé <see cref="AccessType.Livestream"/> và người dùng xem được từ bất cứ đâu.
    /// Người ở Đà Nẵng lọc theo Đà Nẵng đang bị giấu mất đúng những buổi họ hoàn toàn xem được.
    ///
    /// Đã thử nới (<c>s.Format != Offline || khớp thành phố</c>) và đo được hệ quả thật: mọi buổi
    /// trực tuyến trên toàn nền tảng lọt vào MỌI danh sách lọc theo thành phố, làm loãng hẳn kết
    /// quả địa phương. Quan trọng hơn, đường duyệt và tìm kiếm vẫn giữ nghĩa cũ của "thành phố",
    /// nên nới riêng ở đây sẽ khiến hai bên hiểu khác nhau về cùng một tham số — đúng lớp lỗi
    /// MLACP-322 vừa sửa xong.
    ///
    /// Đây là quyết định về SẢN PHẨM (một bộ lọc địa điểm nghĩa là gì), phải chốt một lần cho mọi
    /// màn hình chứ không phải lặng lẽ đổi ở một endpoint. Giữ nguyên nghĩa cũ cho tới khi có quyết
    /// định; gom vào đây để lúc đổi chỉ phải sửa đúng một dòng.
    /// </summary>
    public static Expression<Func<LoungeShow, bool>> ReachableFrom(string city)
        => s => s.Lounge.Address.City == city;
}

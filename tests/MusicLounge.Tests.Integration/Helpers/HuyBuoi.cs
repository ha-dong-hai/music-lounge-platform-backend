namespace MusicLounge.Tests.Integration.Helpers;

/// <summary>
/// MLACP-676. Chủ phòng trà huỷ buổi hòa nhạc ĐÃ MỞ BÁN phải gửi lý do. Các bài test về hậu quả của việc huỷ (hoàn tiền,
/// đơn đồ uống, khoá…) không bàn tới lý do — chúng dùng một lý do hợp lệ chung này.
/// </summary>
public static class HuyBuoi
{
    public static readonly object LyDo = new
    {
        Reason = "VenueIncident",
        Detail = "Mất điện toàn khu vực, điện lực báo không khắc phục kịp trước giờ diễn."
    };
}

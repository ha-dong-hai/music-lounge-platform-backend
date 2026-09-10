using System.Globalization;

namespace MusicLounge.Application.Common;

/// <summary>
/// MLACP-358 — mọi mốc thời gian in vào chữ gửi tới người dùng (thông báo, thông báo lỗi, prompt)
/// đi qua đây để ra giờ Việt Nam.
///
/// <para>Hệ thống tính mốc thời gian theo UTC (<c>DateTimeOffset.UtcNow</c>). Chuỗi định dạng của
/// <see cref="DateTimeOffset"/> in giờ theo chính offset của giá trị, nên một mốc +00:00 in ra giờ
/// UTC — chậm 7 tiếng so với đồng hồ người đọc đang nhìn: "phạt có hiệu lực từ 14:00" trong khi
/// thật ra là 21:00. Với mốc chỉ in ngày, lệch cả ngày khi mốc rơi vào 17h–24h UTC.</para>
///
/// <para>Việt Nam không dùng giờ mùa hè, nên offset cố định +07:00 là đủ — không phụ thuộc múi
/// giờ của máy chủ. <see cref="CultureInfo.InvariantCulture"/> để dấu "/" luôn là "/", không bị
/// thay bằng dấu ngăn cách ngày của culture máy chủ.</para>
/// </summary>
public static class VietnamTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static string Format(DateTimeOffset value, string format = "dd/MM/yyyy HH:mm")
        => value.ToOffset(Offset).ToString(format, CultureInfo.InvariantCulture);
}

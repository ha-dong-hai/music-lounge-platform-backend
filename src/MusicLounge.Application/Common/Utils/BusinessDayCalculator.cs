namespace MusicLounge.Application.Common.Utils;

// D18 (NĐ 144/2020/NĐ-CP Điều 10): show bán vé phải nộp duyệt trước tối thiểu 7 ngày làm việc
// so với ScheduledStart. "Ngày làm việc" ở đây chỉ loại trừ Thứ 7/Chủ nhật — hệ thống chưa có
// lịch ngày lễ nên KHÔNG trừ ngày lễ (giới hạn đã biết, không suy diễn thêm dữ liệu không có).
public static class BusinessDayCalculator
{
    public static int CountBusinessDaysBetween(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from) return 0;

        // MLACP-368: "ngày" là ngày theo lịch Việt Nam. Trước đây lấy .Date của chính offset mỗi giá trị
        // mang theo — nơi gọi truyền DateTimeOffset.UtcNow, nên từ 0h–7h sáng giờ Việt Nam "hôm nay" vẫn là
        // hôm qua và phòng trà được tính dư một ngày làm việc; giờ diễn gửi dạng UTC rơi vào 0h–7h thì bị
        // tính thiếu một ngày. Ngày đầu không tính, bắt đầu từ ngày tiếp theo — BLĐS 2015 Điều 147.
        var count = 0;
        var cursor = from.ToOffset(VietnamTime.Offset).Date.AddDays(1);
        var end = to.ToOffset(VietnamTime.Offset).Date;

        while (cursor <= end)
        {
            if (cursor.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                count++;
            cursor = cursor.AddDays(1);
        }

        return count;
    }
}

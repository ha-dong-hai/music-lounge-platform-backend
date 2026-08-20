namespace MusicLounge.Application.Common.Utils;

// D18 (NĐ 144/2020/NĐ-CP Điều 10): show bán vé phải nộp duyệt trước tối thiểu 7 ngày làm việc
// so với ScheduledStart. "Ngày làm việc" ở đây chỉ loại trừ Thứ 7/Chủ nhật — hệ thống chưa có
// lịch ngày lễ nên KHÔNG trừ ngày lễ (giới hạn đã biết, không suy diễn thêm dữ liệu không có).
public static class BusinessDayCalculator
{
    public static int CountBusinessDaysBetween(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from) return 0;

        var count = 0;
        var cursor = from.Date.AddDays(1);
        var end = to.Date;

        while (cursor <= end)
        {
            if (cursor.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                count++;
            cursor = cursor.AddDays(1);
        }

        return count;
    }
}

using FluentAssertions;
using MusicLounge.Application.Common.Utils;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-368. Hạn nộp duyệt buổi diễn tính bằng ngày làm việc (NĐ 144/2020 Điều 10 khoản 5: tối thiểu 07
/// ngày làm việc). Cách đếm theo đúng BLĐS 2015 Điều 147 — ngày đầu không tính, bắt đầu từ 0 giờ ngày tiếp
/// theo — nhưng "ngày" từng là ngày theo <b>UTC</b>: từ 0h đến 7h sáng giờ Việt Nam, "hôm nay" theo UTC vẫn là
/// hôm qua, nên phòng trà được tính dư một ngày làm việc; giờ diễn gửi dạng UTC rơi vào 0h–7h Việt Nam thì
/// bị tính thiếu một ngày.
///
/// <para>Gọi thẳng hàm đếm với mốc thời gian cố định: lỗi chỉ lộ ra trong một khung giờ trong ngày, nên một
/// bài đi qua API sẽ xanh hay đỏ tuỳ lúc máy chạy test.</para>
/// </summary>
public sealed class BusinessDaysOnTheVietnameseCalendarTests
{
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);

    [Fact]
    public void JustAfterMidnightInVietnam_TodayIsNotCountedAsAFutureDay()
    {
        // Thứ Hai 14/09/2026 lúc 01:00 giờ Việt Nam — theo UTC vẫn là Chủ nhật 13/09, 18:00.
        var submittedAt = new DateTimeOffset(2026, 9, 13, 18, 0, 0, TimeSpan.Zero);
        var showStart = new DateTimeOffset(2026, 9, 15, 20, 0, 0, Vn); // Thứ Ba

        BusinessDayCalculator.CountBusinessDaysBetween(submittedAt, showStart).Should().Be(1,
            "ngày nộp (thứ Hai theo lịch Việt Nam) không được tính — chỉ còn thứ Ba");
    }

    [Fact]
    public void AShowStartSentInUtc_IsCountedOnItsVietnameseDate()
    {
        var submittedAt = new DateTimeOffset(2026, 9, 14, 10, 0, 0, Vn); // Thứ Hai
        // 18:30 UTC thứ Ba = 01:30 sáng thứ Tư giờ Việt Nam.
        var showStart = new DateTimeOffset(2026, 9, 15, 18, 30, 0, TimeSpan.Zero);

        BusinessDayCalculator.CountBusinessDaysBetween(submittedAt, showStart).Should().Be(2,
            "buổi diễn rơi vào thứ Tư theo lịch Việt Nam — thứ Ba và thứ Tư");
    }

    [Fact]
    public void TheSameInstants_CountTheSame_WhateverOffsetTheyArriveIn()
    {
        // Chỉ mốc nộp rơi vào 0h–7h giờ Việt Nam. Nếu cả hai mốc cùng rơi vào khung đó thì cả hai cùng lệch
        // một ngày và kết quả trùng nhau một cách tình cờ — bài kiểm tra sẽ xanh cả trên code sai.
        var submittedVn = new DateTimeOffset(2026, 9, 14, 1, 0, 0, Vn);
        var startVn = new DateTimeOffset(2026, 9, 23, 15, 0, 0, Vn);

        BusinessDayCalculator.CountBusinessDaysBetween(submittedVn.ToUniversalTime(), startVn.ToUniversalTime())
            .Should().Be(BusinessDayCalculator.CountBusinessDaysBetween(submittedVn, startVn),
                "cùng hai thời điểm thì cùng một số ngày — không phụ thuộc offset lúc nhận");
    }
}

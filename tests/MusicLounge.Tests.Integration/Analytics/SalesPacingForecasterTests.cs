using FluentAssertions;
using MusicLounge.Application.Analytics.Common;

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-311. Phần lõi của dự báo nhu cầu vé, tách khỏi database để kiểm được đúng cái cần kiểm:
/// phép tính, và nhất là các trường hợp nó phải TỪ CHỐI trả lời.
///
/// Yêu cầu trong đăng ký đề tài là "AI dự đoán xu hướng". Rủi ro lớn nhất của một tính năng như vậy
/// không phải là sai số — mà là nó luôn trả về một con số trông có vẻ đáng tin kể cả khi không có
/// gì để dựa vào. Chủ phòng trà đọc con số đó rồi xếp lịch, và thiệt hại là thật.
///
/// Nên phân nửa số test ở đây kiểm rằng nó im lặng đúng lúc.
/// </summary>
public sealed class SalesPacingForecasterTests
{
    /// <summary>Buổi diễn cũ bán tổng <paramref name="final"/> vé, tới mốc đang xét đã bán được
    /// <paramref name="fraction"/> phần trong số đó.</summary>
    private static ReferenceShowPace Show(int final, double fraction)
        => new(final, (int)Math.Round(final * fraction));

    private static List<ReferenceShowPace> Shows(int count, int final, double fraction)
        => Enumerable.Range(0, count).Select(_ => Show(final, fraction)).ToList();

    // ---------- khi nào thì im lặng ----------

    [Fact]
    public void WithTwoCompletedShows_ItRefusesToGuess()
    {
        // Ngưỡng 3 không phải sự thận trọng tuỳ hứng: dưới mức đó thì chính tham số hiệu chỉnh
        // mẫu nhỏ cũng không ước lượng nổi, nên con số trả ra không còn hiệu chuẩn.
        var history = Shows(2, final: 100, fraction: 0.5);

        var result = SalesPacingForecaster.Forecast(history, history, ticketsSoldSoFar: 40, capacity: null);

        result.Status.Should().Be(ForecastStatus.NotEnoughHistory);
        result.ProjectedFinalSales.Should().BeNull("không có số còn hơn có số sai");
    }

    [Fact]
    public void WithNoHistoryAtAll_ItRefusesToGuess()
    {
        var result = SalesPacingForecaster.Forecast([], [], ticketsSoldSoFar: 0, capacity: 100);

        result.Status.Should().Be(ForecastStatus.NotEnoughHistory);
        result.ProjectedFinalSales.Should().BeNull();
    }

    [Fact]
    public void WhenTheShowIsStillFarOff_ItSaysSoInsteadOfInflatingTheNumber()
    {
        // Lịch sử cho thấy tới mốc này mới bán được 2% tổng vé. Chia doanh số hiện tại cho 0.02 là
        // nhân nó lên 50 lần — và nhân sai số của nó lên 50 lần theo. 3 vé đã bán thành "dự báo
        // 150 vé", một con số không có cơ sở nào.
        var history = Shows(5, final: 100, fraction: 0.02);

        var result = SalesPacingForecaster.Forecast(history, history, ticketsSoldSoFar: 3, capacity: null);

        result.Status.Should().Be(ForecastStatus.TooEarly);
        result.ProjectedFinalSales.Should().BeNull();
        result.ExpectedPaceFraction.Should().BeApproximately(0.02m, 0.001m,
            "vẫn nói ra tỉ lệ đang thấy, để người đọc hiểu vì sao chưa dự báo được");
    }

    // ---------- phép tính ----------

    [Fact]
    public void HalfwayThroughTheUsualSalesCurve_ItProjectsDouble()
    {
        // Cả năm buổi diễn cũ tới mốc này đều đã bán được đúng nửa tổng vé. Buổi đang xét bán được
        // 40 vé, nên tổng dự kiến là 80. Đây là toàn bộ phương pháp, viết ra thành một phép chia.
        var history = Shows(5, final: 100, fraction: 0.5);

        var result = SalesPacingForecaster.Forecast(history, history, ticketsSoldSoFar: 40, capacity: null);

        result.Status.Should().Be(ForecastStatus.Forecast);
        result.ProjectedFinalSales.Should().Be(80);
    }

    [Fact]
    public void TheConfidenceBandComesFromHowMuchThePastShowsDisagree()
    {
        // Ba buổi diễn cũ, nhịp bán rất khác nhau: 40%, 50%, 80%. Cùng 40 vé đã bán sẽ cho ba dự
        // báo 100, 80, 50 — và bề rộng đó chính là mức không chắc chắn thật sự. Không bịa ra một
        // biên độ phần trăm cố định.
        List<ReferenceShowPace> history =
            [Show(100, 0.4), Show(100, 0.5), Show(100, 0.8)];

        var result = SalesPacingForecaster.Forecast(history, history, ticketsSoldSoFar: 40, capacity: null);

        result.ProjectedLow.Should().Be(50);
        result.ProjectedHigh.Should().Be(100);
        result.ProjectedFinalSales.Should().BeInRange(result.ProjectedLow!.Value, result.ProjectedHigh!.Value);
    }

    // ---------- xử lý mẫu nhỏ ----------

    [Fact]
    public void AVenueWithNoTrackRecord_LeansEntirelyOnThePlatform()
    {
        // Phòng trà mới toanh: không có gì của riêng nó để dựa vào, nên dùng hẳn nhịp bán chung.
        var platform = Shows(10, final: 100, fraction: 0.25);

        var result = SalesPacingForecaster.Forecast([], platform, ticketsSoldSoFar: 25, capacity: null);

        result.VenueHistoryWeight.Should().Be(0m);
        result.ProjectedFinalSales.Should().Be(100);
    }

    [Fact]
    public void AVenueWithALongTrackRecord_MostlyTrustsItself()
    {
        // 27 buổi diễn của riêng nó thì trọng số là 27/(27+3) = 90%. Nhịp riêng 50%, nhịp chung
        // 25%, nên nhịp dùng để tính là 0.9*0.5 + 0.1*0.25 = 0.475 → 40/0.475 ≈ 84.
        var venue = Shows(27, final: 100, fraction: 0.5);
        var platform = Shows(50, final: 100, fraction: 0.25);

        var result = SalesPacingForecaster.Forecast(venue, platform, ticketsSoldSoFar: 40, capacity: null);

        result.VenueHistoryWeight.Should().BeApproximately(0.9m, 0.001m);
        result.ProjectedFinalSales.Should().Be(84);
    }

    [Fact]
    public void AVenueAtExactlyTheMinimum_WeighsItselfAndThePlatformEqually()
    {
        // 3 buổi diễn: 3/(3+3) = 50%. Đây là ý nghĩa của hằng số hiệu chỉnh — chọn nó bằng đúng
        // ngưỡng tối thiểu để "vừa đủ để tự làm chuẩn" nghĩa là "ngang bằng với nền chung".
        var venue = Shows(3, final: 100, fraction: 0.5);
        var platform = Shows(20, final: 100, fraction: 0.3);

        var result = SalesPacingForecaster.Forecast(venue, platform, ticketsSoldSoFar: 40, capacity: null);

        result.VenueHistoryWeight.Should().Be(0.5m);
        result.ExpectedPaceFraction.Should().BeApproximately(0.4m, 0.001m);
    }

    // ---------- những con số không được phép trả ra ----------

    [Fact]
    public void ItNeverProjectsFewerTicketsThanAlreadySold()
    {
        // Đã bán 90 vé rồi thì dự báo 50 là một câu vô nghĩa, dù phép chia có ra thế nào.
        var history = Shows(5, final: 100, fraction: 0.95);

        var result = SalesPacingForecaster.Forecast(history, history, ticketsSoldSoFar: 90, capacity: null);

        result.ProjectedFinalSales.Should().BeGreaterThanOrEqualTo(90);
        result.ProjectedLow.Should().BeGreaterThanOrEqualTo(90);
    }

    [Fact]
    public void ItNeverProjectsMoreTicketsThanTheRoomHolds()
    {
        // Phòng 100 chỗ thì không bán được 160 vé, dù nhịp bán đang rất tốt.
        var history = Shows(5, final: 100, fraction: 0.25);

        var result = SalesPacingForecaster.Forecast(history, history, ticketsSoldSoFar: 40, capacity: 100);

        result.ProjectedFinalSales.Should().Be(100);
        result.ProjectedHigh.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void ShowsThatSoldNothing_AreLeftOutInsteadOfDividingByZero()
    {
        // Một buổi diễn không bán được vé nào thì không nói lên điều gì về nhịp bán. Ba buổi hợp lệ
        // vẫn đủ ngưỡng, và buổi rỗng không được kéo trung bình đi đâu cả.
        List<ReferenceShowPace> history =
            [Show(100, 0.5), Show(100, 0.5), Show(100, 0.5), new ReferenceShowPace(0, 0)];

        var result = SalesPacingForecaster.Forecast(history, history, ticketsSoldSoFar: 40, capacity: null);

        result.Status.Should().Be(ForecastStatus.Forecast);
        result.ProjectedFinalSales.Should().Be(80);
    }

    [Fact]
    public void ThreeShowsOfWhichOneIsEmpty_FallsBackBelowTheThreshold()
    {
        // Và nếu bỏ buổi rỗng đi thì chỉ còn 2 buổi dùng được, tức là dưới ngưỡng — phải im lặng,
        // chứ không phải cứ đủ 3 dòng dữ liệu là dự báo.
        List<ReferenceShowPace> history =
            [Show(100, 0.5), Show(100, 0.5), new ReferenceShowPace(0, 0)];

        var result = SalesPacingForecaster.Forecast(history, history, ticketsSoldSoFar: 40, capacity: null);

        result.Status.Should().Be(ForecastStatus.NotEnoughHistory);
    }

    [Fact]
    public void NothingSoldYet_ProjectsNothingRatherThanExploding()
    {
        // 0 chia cho bất kỳ tỉ lệ nào vẫn là 0. Trường hợp tầm thường, nhưng nó là đầu vào thật
        // của mọi buổi diễn vừa mở bán.
        var history = Shows(5, final: 100, fraction: 0.5);

        var result = SalesPacingForecaster.Forecast(history, history, ticketsSoldSoFar: 0, capacity: 100);

        result.Status.Should().Be(ForecastStatus.Forecast);
        result.ProjectedFinalSales.Should().Be(0);
    }
}

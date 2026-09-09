using FluentAssertions;
using MusicLounge.Application.Analytics.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-315. Phần lõi của bảng "đang được quan tâm", tách khỏi database.
///
/// Bảng cũ là một phép đếm: số bản ghi hành vi trong 7 ngày, không phân biệt loại, cắt cứng ở mốc
/// 7 ngày. Nó chưa từng có test nào kiểm thứ tự xếp hạng — chỉ có test kiểm rằng nó không ném lỗi
/// khi truyền limit âm. Đó là lý do ba khiếm khuyết dưới đây sống sót.
///
/// Mỗi test ở đây ứng với đúng một trong ba, viết dưới dạng tình huống thật chứ không phải kiểm
/// công thức: điều đáng bảo vệ là "bảng xếp hạng nói đúng cái gì đang hot", không phải "hàm trả về
/// đúng số thập phân".
/// </summary>
public sealed class TrendingScorerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    private static TrendingEvent Ev(int userId, TrendingSignal signal, double hoursAgo)
        => new($"user:{userId}", signal, Now.AddHours(-hoursAgo));

    /// <summary>Nhiều người khác nhau cùng làm một việc, tất cả vừa xong.</summary>
    private static List<TrendingEvent> Crowd(int count, TrendingSignal signal, double hoursAgo = 1)
        => Enumerable.Range(1, count).Select(i => Ev(i, signal, hoursAgo)).ToList();

    // ---------- khiếm khuyết 1: mọi hành động nặng như nhau ----------

    [Fact]
    public async Task TenTicketsSold_OutranksFiftyPeopleGlancingAtThePage()
    {
        // Bảng cũ xếp ngược lại: 50 lượt xem = 50 điểm, 10 vé = 10 điểm. Buổi diễn bán được vé
        // thật bị đẩy xuống dưới buổi diễn chỉ được liếc qua.
        var soldTickets = TrendingScorer.Score(Crowd(10, TrendingSignal.Purchase), Now);
        var justLooking = TrendingScorer.Score(Crowd(50, TrendingSignal.Browse), Now);

        soldTickets.Should().BeGreaterThan(justLooking,
            "vé bán được là bằng chứng duy nhất mà người dùng phải trả tiền để tạo ra");
        await Task.CompletedTask;
    }

    [Fact]
    public void TheSignalsAreOrderedByHowCloseTheyAreToBuying()
    {
        // Ghim lại chính thang thứ tự, vì đây là chỗ dễ bị chỉnh tuỳ tiện nhất về sau.
        var order = new[]
        {
            TrendingSignal.Browse, TrendingSignal.Attention, TrendingSignal.Save,
            TrendingSignal.Intent, TrendingSignal.Purchase
        };

        var weights = order.Select(TrendingScorer.WeightOf).ToList();

        weights.Should().BeInAscendingOrder();
        TrendingScorer.WeightOf(TrendingSignal.Share)
            .Should().BeGreaterThan(TrendingScorer.WeightOf(TrendingSignal.Browse),
                "chia sẻ cho người khác tốn công hơn hẳn việc mở trang lên xem");
    }

    // ---------- khiếm khuyết 2: cắt cứng, không suy giảm ----------

    [Fact]
    public void AShowThatWasHotLastWeekLosesToOneThatIsHotNow()
    {
        // Đây là khác biệt giữa "được chú ý nhiều" và "đang được chú ý" — tức là giữa popular và
        // trending. Bảng cũ không phân biệt được: cả hai đều nằm trong cửa sổ 7 ngày nên đếm ngang
        // nhau, và buổi diễn đã nguội vẫn nằm trên đầu bảng.
        var coolingOff = TrendingScorer.Score(Crowd(20, TrendingSignal.Browse, hoursAgo: 6 * 24), Now);
        var heatingUp = TrendingScorer.Score(Crowd(20, TrendingSignal.Browse, hoursAgo: 2), Now);

        heatingUp.Should().BeGreaterThan(coolingOff);
    }

    [Fact]
    public void ThereIsNoCliffEdgeAtAnyParticularAge()
    {
        // Bảng cũ có một vách đứng: 6 ngày 23 giờ tính đủ điểm, 7 ngày 1 giờ tính bằng 0. Hai tín
        // hiệu cách nhau hai tiếng không được cho ra kết quả khác nhau một trời một vực.
        var justInside = TrendingScorer.Score([Ev(1, TrendingSignal.Browse, 7 * 24 - 1)], Now);
        var justOutside = TrendingScorer.Score([Ev(1, TrendingSignal.Browse, 7 * 24 + 1)], Now);

        justOutside.Should().BeGreaterThan(0, "sự quan tâm cũ mờ dần chứ không biến mất đột ngột");
        justOutside.Should().BeApproximately(justInside, justInside * 0.05,
            "chênh hai tiếng thì điểm chỉ được chênh vài phần trăm");
    }

    [Fact]
    public void InterestLosesHalfItsWeightEveryThreeDays()
    {
        // Ghim tham số vì nó quyết định bảng xếp hạng "tươi" tới mức nào. Đổi nó là đổi hành vi
        // sản phẩm, phải là một quyết định có ý thức.
        var fresh = TrendingScorer.Score([Ev(1, TrendingSignal.Purchase, 0)], Now);
        var threeDaysOld = TrendingScorer.Score([Ev(1, TrendingSignal.Purchase, 72)], Now);

        threeDaysOld.Should().BeApproximately(fresh / 2, 0.001);
    }

    // ---------- khiếm khuyết 3: thổi số bằng cách bấm lại ----------

    [Fact]
    public void OnePersonRefreshingFiftyTimesCountsOnce()
    {
        // Không có bước này thì bảng xếp hạng đo được ai bấm F5 nhiều nhất, và bất kỳ chủ phòng trà
        // nào cũng tự đẩy buổi diễn của mình lên đầu trong năm phút.
        var oneKeenPerson = Enumerable.Range(0, 50)
            .Select(i => Ev(userId: 1, TrendingSignal.Browse, hoursAgo: i * 0.05)).ToList();

        var fiftyDifferentPeople = Crowd(50, TrendingSignal.Browse);

        // So với đúng một lượt xem tại thời điểm SỚM NHẤT trong loạt đó: bộ gộp trùng giữ lần đầu,
        // vì đó là lúc sự quan tâm thật sự xuất hiện — những lần bấm lại sau không thêm thông tin gì.
        var singleVisitAtTheSameMoment = TrendingScorer.Score(
            [Ev(1, TrendingSignal.Browse, hoursAgo: 49 * 0.05)], Now);

        TrendingScorer.Score(oneKeenPerson, Now)
            .Should().BeApproximately(singleVisitAtTheSameMoment, 0.0001,
                "năm mươi lần bấm của cùng một người trong cùng một ngày chỉ đáng giá một lần");

        TrendingScorer.Score(fiftyDifferentPeople, Now)
            .Should().BeGreaterThan(TrendingScorer.Score(oneKeenPerson, Now) * 40,
                "năm mươi người quan tâm khác hẳn một người bấm năm mươi lần");
    }

    [Fact]
    public void ComingBackTheNextDayDoesCountAgain()
    {
        // Gộp theo ngày chứ không gộp cả kỳ: người quay lại vào hôm sau là tín hiệu thật.
        var sameDay = new List<TrendingEvent> { Ev(1, TrendingSignal.Browse, 1), Ev(1, TrendingSignal.Browse, 3) };
        var twoDays = new List<TrendingEvent> { Ev(1, TrendingSignal.Browse, 1), Ev(1, TrendingSignal.Browse, 25) };

        TrendingScorer.Score(twoDays, Now)
            .Should().BeGreaterThan(TrendingScorer.Score(sameDay, Now));
    }

    [Fact]
    public void TheSamePersonBrowsingThenBuying_CountsAsBoth()
    {
        // Gộp trùng theo TỪNG loại tín hiệu, không phải theo người. Xem rồi mua là hai bước khác
        // nhau của cùng một người, và bước thứ hai mới là bước đáng giá.
        var browsedThenBought = new List<TrendingEvent>
        {
            Ev(1, TrendingSignal.Browse, 2),
            Ev(1, TrendingSignal.Purchase, 1)
        };

        TrendingScorer.Score(browsedThenBought, Now)
            .Should().BeGreaterThan(TrendingScorer.Score([Ev(1, TrendingSignal.Purchase, 1)], Now));
    }

    // ---------- các trường hợp biên ----------

    [Fact]
    public void AShowNobodyHasLookedAtScoresZero()
    {
        // 0 là câu trả lời hợp lệ, không phải lỗi — phần lớn buổi diễn trên một nền tảng mới đều
        // như vậy, và bảng xếp hạng phải xử lý được điều đó một cách bình thường.
        TrendingScorer.Score([], Now).Should().Be(0);
    }

    [Fact]
    public void ASignalTimestampedInTheFutureDoesNotEarnBonusPoints()
    {
        // Lệch đồng hồ giữa các máy là chuyện có thật. Không chặn thì 0.5^(số âm) thành số mũ
        // dương, và một bản ghi lệch giờ đủ để chiếm đỉnh bảng.
        var future = TrendingScorer.Score([Ev(1, TrendingSignal.Purchase, hoursAgo: -240)], Now);
        var justNow = TrendingScorer.Score([Ev(1, TrendingSignal.Purchase, hoursAgo: 0)], Now);

        future.Should().BeApproximately(justNow, 0.001);
    }

    [Fact]
    public void EveryLoggedActionMapsToASignal_SoNothingIsSilentlyDropped()
    {
        // Nếu ai đó thêm một hành động mới vào nhật ký mà quên khai ở đây, nó sẽ lặng lẽ không được
        // tính và không có gì báo. Test này là chỗ báo.
        foreach (var action in Enum.GetValues<BehaviourAction>())
            TrendingScorer.FromBehaviour(action).Should().NotBeNull(
                $"hành động {action} được ghi vào nhật ký nhưng không có trọng số nào");
    }
}

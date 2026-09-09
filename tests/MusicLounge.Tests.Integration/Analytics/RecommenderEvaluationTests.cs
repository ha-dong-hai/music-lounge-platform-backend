using FluentAssertions;
using MusicLounge.Application.Analytics.Common;

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-317. Phép đánh giá offline cho hệ gợi ý, tách khỏi database.
///
/// Trước bộ này, không ai — kể cả người viết ra hệ gợi ý — nói được một thay đổi làm gợi ý tốt lên
/// hay tệ đi. Hệ thống có thước đo online (CTR/chuyển đổi) nhưng nó chỉ có nghĩa sau khi đã có
/// người dùng thật bấm vào, và nó không so được mô hình này với mô hình khác.
///
/// Một phép đo sai còn tệ hơn không đo, vì nó tạo ra sự tự tin không có cơ sở. Nên bộ test này kiểm
/// chính phép đo: nó có thưởng đúng mô hình tốt, phạt đúng mô hình tệ, và có im lặng khi dữ liệu
/// quá mỏng hay không.
/// </summary>
public sealed class RecommenderEvaluationTests
{
    /// <summary>Người dùng <paramref name="userId"/> thật sự đã chọn <paramref name="heldOut"/>,
    /// trộn giữa các lựa chọn nhiễu.</summary>
    private static EvaluationCase Case(int userId, int heldOut, params int[] negatives)
        => new(userId, heldOut, [.. negatives, heldOut]);

    /// <summary>Mô hình hoàn hảo: luôn đặt đáp án lên đầu.</summary>
    private static Func<int, IReadOnlyList<int>, IReadOnlyList<int>> Oracle(
        IReadOnlyDictionary<int, int> answerByUser)
        => (userId, candidates) => candidates
            .OrderByDescending(id => id == answerByUser[userId])
            .ToList();

    /// <summary>Mô hình tệ nhất có thể: luôn đẩy đáp án xuống cuối.</summary>
    private static Func<int, IReadOnlyList<int>, IReadOnlyList<int>> Adversary(
        IReadOnlyDictionary<int, int> answerByUser)
        => (userId, candidates) => candidates
            .OrderBy(id => id == answerByUser[userId])
            .ToList();

    // ---------- phép đo có phân biệt được tốt với tệ không ----------

    [Fact]
    public void AModelThatAlwaysGetsItRight_ScoresOneHundredPercent()
    {
        var answers = new Dictionary<int, int> { [1] = 10, [2] = 20 };
        List<EvaluationCase> cases = [Case(1, 10, 11, 12, 13), Case(2, 20, 21, 22, 23)];

        var result = RecommenderEvaluation.Evaluate(
            "oracle", cases, Oracle(answers), k: 1, catalogueSize: 100);

        result.HitRateAtK.Should().Be(1.0);
        result.Hits.Should().Be(2);
    }

    [Fact]
    public void AModelThatAlwaysGetsItWrong_ScoresZero()
    {
        // Nếu phép đo không phạt được mô hình tệ nhất có thể thì nó không đo gì cả.
        var answers = new Dictionary<int, int> { [1] = 10, [2] = 20 };
        List<EvaluationCase> cases = [Case(1, 10, 11, 12, 13), Case(2, 20, 21, 22, 23)];

        var result = RecommenderEvaluation.Evaluate(
            "adversary", cases, Adversary(answers), k: 1, catalogueSize: 100);

        result.HitRateAtK.Should().Be(0.0);
    }

    [Fact]
    public void LookingAtMoreSlotsCanOnlyHelp()
    {
        // HR@K không bao giờ giảm khi K tăng. Tính chất hiển nhiên của định nghĩa, và cũng là phép
        // thử nhanh nhất để phát hiện phép đo bị cài sai.
        var answers = new Dictionary<int, int> { [1] = 10 };
        List<EvaluationCase> cases = [Case(1, 10, 11, 12, 13, 14)];

        var ranker = Adversary(answers);
        var atOne = RecommenderEvaluation.Evaluate("m", cases, ranker, 1, 100).HitRateAtK;
        var atFive = RecommenderEvaluation.Evaluate("m", cases, ranker, 5, 100).HitRateAtK;

        atOne.Should().BeLessThanOrEqualTo(atFive);
        atFive.Should().Be(1.0, "5 chỗ mà chỉ có 5 ứng viên thì đáp án chắc chắn lọt vào");
    }

    // ---------- baseline độ phổ biến ----------

    [Fact]
    public void ThePopularityBaselinePutsTheMostChosenShowFirst()
    {
        var counts = new Dictionary<int, int> { [10] = 1, [11] = 50, [12] = 5 };

        var ranked = RecommenderEvaluation.PopularityRanker(counts)(1, [10, 11, 12]);

        ranked.Should().ContainInOrder(11, 12, 10);
    }

    [Fact]
    public void PopularityLooksGoodOnCommonChoices_AndBlindOnTheRest()
    {
        // Đây là lý do baseline này vừa cần thiết vừa nguy hiểm: nó thắng dễ khi ai cũng chọn cùng
        // một thứ, nhưng hoàn toàn mù với người có gu riêng. Con số HR@K của nó phải được đọc kèm
        // độ phủ kho, nếu không sẽ dẫn tới kết luận sai là "cá nhân hoá không cần thiết".
        var counts = new Dictionary<int, int> { [99] = 1000 };
        var ranker = RecommenderEvaluation.PopularityRanker(counts);

        var mainstream = RecommenderEvaluation.Evaluate(
            "pop", [Case(1, 99, 1, 2, 3)], ranker, k: 1, catalogueSize: 100);
        var nicheTaste = RecommenderEvaluation.Evaluate(
            "pop", [Case(2, 3, 99, 1, 2)], ranker, k: 1, catalogueSize: 100);

        mainstream.HitRateAtK.Should().Be(1.0);
        nicheTaste.HitRateAtK.Should().Be(0.0);
    }

    // ---------- độ phủ kho ----------

    [Fact]
    public void AModelThatKeepsRecommendingTheSameShow_HasTerribleCoverage()
    {
        // Độ phủ là thứ phơi bày cái giá thật của việc chỉ gợi ý những gì đang hot: điểm HR có thể
        // đẹp trong khi 98% kho không bao giờ được ai nhìn thấy.
        var counts = new Dictionary<int, int> { [99] = 1000 };
        List<EvaluationCase> cases = [Case(1, 99, 1, 2), Case(2, 99, 3, 4)];

        var result = RecommenderEvaluation.Evaluate(
            "pop", cases, RecommenderEvaluation.PopularityRanker(counts), k: 1, catalogueSize: 100);

        result.CatalogueCoverage.Should().Be(0.01, "chỉ đúng 1 trong 100 buổi diễn từng được đề xuất");
    }

    [Fact]
    public void AModelThatSpreadsItsRecommendationsAround_HasBetterCoverage()
    {
        var answers = new Dictionary<int, int> { [1] = 10, [2] = 20 };
        List<EvaluationCase> cases = [Case(1, 10, 11), Case(2, 20, 21)];

        var result = RecommenderEvaluation.Evaluate(
            "oracle", cases, Oracle(answers), k: 1, catalogueSize: 100);

        result.CatalogueCoverage.Should().Be(0.02);
    }

    // ---------- lấy mẫu phải lặp lại được ----------

    [Fact]
    public void TheSameSeedGivesTheSameSample()
    {
        // Con số đưa vào báo cáo mà mỗi lần chạy lại ra một kiểu thì không ai kiểm chứng được, và
        // hai lần đo cũng không so được với nhau.
        List<int> catalogue = [.. Enumerable.Range(1, 200)];
        var seen = new HashSet<int> { 1, 2, 3 };

        var first = RecommenderEvaluation.SampleNegatives(catalogue, seen, 20, new Random(42));
        var second = RecommenderEvaluation.SampleNegatives(catalogue, seen, 20, new Random(42));

        first.Should().Equal(second);
    }

    [Fact]
    public void TheSampleNeverIncludesSomethingTheUserAlreadyChose()
    {
        // Trộn vào một buổi diễn người ta đã từng chọn thì bài kiểm tra có hai đáp án đúng, và điểm
        // số trở nên vô nghĩa.
        List<int> catalogue = [.. Enumerable.Range(1, 50)];
        var seen = new HashSet<int> { 5, 10, 15, 20 };

        var sample = RecommenderEvaluation.SampleNegatives(catalogue, seen, 30, new Random(7));

        sample.Should().NotIntersectWith(seen);
        sample.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AskingForMoreNoiseThanExists_ReturnsWhatThereIs()
    {
        // Kho nhỏ là tình trạng thật của nền tảng này. Không được ném lỗi, chỉ trả về ít hơn — và
        // đường gọi tự quyết định như vậy có đủ để kiểm tra hay không.
        List<int> catalogue = [1, 2, 3, 4, 5];

        RecommenderEvaluation.SampleNegatives(catalogue, new HashSet<int> { 1 }, 99, new Random(1))
            .Should().HaveCount(4);
    }

    // ---------- các trường hợp biên ----------

    [Fact]
    public void NoCasesAtAll_ScoresZeroInsteadOfDividingByZero()
    {
        var result = RecommenderEvaluation.Evaluate(
            "m", [], (_, c) => c, k: 10, catalogueSize: 100);

        result.HitRateAtK.Should().Be(0);
        result.CatalogueCoverage.Should().Be(0);
        result.Cases.Should().Be(0);
    }

    [Fact]
    public void AnEmptyCatalogueDoesNotBreakCoverage()
    {
        RecommenderEvaluation.Evaluate("m", [Case(1, 10, 11)], (_, c) => c, k: 1, catalogueSize: 0)
            .CatalogueCoverage.Should().Be(0);
    }
}

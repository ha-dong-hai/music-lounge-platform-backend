namespace MusicLounge.Application.Analytics.Common;

/// <param name="HeldOutShowId">
/// Buổi diễn người này thật sự đã chọn, bị giấu đi. Mô hình không được biết nó khi xếp hạng.
/// </param>
/// <param name="CandidateShowIds">
/// Buổi diễn bị giấu, trộn lẫn với một số buổi người này chưa từng chạm tới. Mô hình xếp hạng cả
/// tập này; câu hỏi là nó có đẩy được buổi đúng lên đầu không.
/// </param>
public sealed record EvaluationCase(
    int UserId,
    int HeldOutShowId,
    IReadOnlyList<int> CandidateShowIds);

/// <param name="HitRateAtK">
/// Tỉ lệ trường hợp mà buổi diễn bị giấu lọt vào top K. Đây là thước đo chính: nó trả lời đúng câu
/// "mô hình có đoán được thứ người ta thật sự chọn không".
/// </param>
/// <param name="CatalogueCoverage">
/// Tỉ lệ buổi diễn trong kho từng được mô hình đưa vào top K của ít nhất một người. Thấp nghĩa là
/// mô hình chỉ quanh quẩn vài buổi diễn quen thuộc — thước đo phổ biến nhất luôn có độ phủ tệ, và
/// đó là cái giá thật của việc chỉ gợi ý thứ đang hot.
/// </param>
public sealed record ModelEvaluation(
    string Model,
    int Cases,
    int Hits,
    double HitRateAtK,
    double CatalogueCoverage);

/// <summary>
/// Đánh giá offline cho hệ gợi ý — trả lời câu "AI của bạn tốt tới mức nào" bằng số, ngay cả khi
/// chưa có lưu lượng người dùng thật.
///
/// <b>Vì sao cần, khi đã có thước đo online.</b> Hệ thống đã đo CTR và tỉ lệ chuyển đổi trên các
/// gợi ý đã hiển thị. Nhưng thước đo đó chỉ có nghĩa SAU KHI đã có người dùng thật bấm vào, và nó
/// chỉ nói về những gợi ý đã được đưa ra — nó không so được mô hình hiện tại với một mô hình khác.
/// Không có đánh giá offline thì không ai, kể cả người viết ra nó, biết được một thay đổi làm gợi ý
/// tốt lên hay tệ đi.
///
/// <b>Cách làm — leave-one-out, đúng chuẩn dùng trong tài liệu ngành.</b> Với mỗi người dùng, giấu
/// đi tương tác GẦN NHẤT của họ, trộn nó với một số buổi diễn họ chưa từng chạm, rồi bắt mô hình
/// xếp hạng cả tập đó. <b>HR@K</b> là tỉ lệ trường hợp buổi bị giấu lọt vào top K. Giấu tương tác
/// gần nhất chứ không phải một cái ngẫu nhiên: mô hình được dùng để đoán việc sắp xảy ra, nên phép
/// đo phải mô phỏng đúng tình huống đó.
///
/// <b>Và luôn so với một baseline.</b> Một con số HR@K đứng một mình không nói lên điều gì —
/// 0.4 là tốt hay tệ? Câu trả lời chỉ có nghĩa khi đặt cạnh phép gợi ý ngây thơ nhất: cứ đưa ra
/// buổi diễn nhiều người chọn nhất. Mô hình cá nhân hoá không vượt được baseline đó thì nó không
/// đáng với độ phức tạp nó mang lại, và đó là một kết luận đáng biết chứ không phải đáng giấu.
///
/// <b>Cảnh báo phải nói kèm mọi con số.</b> Đánh giá offline có thiên lệch theo độ phổ biến: một
/// buổi diễn có nhiều tương tác một phần vì chính hệ thống đã đẩy nó ra cho nhiều người xem. Nên
/// baseline phổ biến thường được lợi một cách giả tạo trong phép đo này, và việc mô hình cá nhân
/// hoá chỉ ngang ngửa nó chưa chắc là thất bại. Đó cũng là lý do phải báo cáo thêm độ phủ kho.
/// </summary>
public static class RecommenderEvaluation
{
    /// <summary>
    /// Số người dùng tối thiểu có đủ lịch sử để phép đo còn có nghĩa. Dưới mức này thì mỗi người
    /// đúng hay sai làm điểm số nhảy hàng chục phần trăm, và con số đó không nói lên điều gì về
    /// mô hình.
    /// </summary>
    public const int MinimumCases = 10;

    /// <param name="rank">
    /// Mô hình cần đánh giá: nhận id người dùng và tập ứng viên, trả về chính tập đó đã xếp theo
    /// thứ tự nó cho là đúng. Để mô hình ở dạng tham số nên thêm một mô hình mới để so là thêm một
    /// hàm, không phải sửa phép đo.
    /// </param>
    /// <param name="catalogueSize">Tổng số buổi diễn có thể được gợi ý, để tính độ phủ.</param>
    public static ModelEvaluation Evaluate(
        string model,
        IReadOnlyList<EvaluationCase> cases,
        Func<int, IReadOnlyList<int>, IReadOnlyList<int>> rank,
        int k,
        int catalogueSize)
    {
        var hits = 0;
        var recommendedAtLeastOnce = new HashSet<int>();

        foreach (var c in cases)
        {
            var ranked = rank(c.UserId, c.CandidateShowIds).Take(k).ToList();

            recommendedAtLeastOnce.UnionWith(ranked);

            if (ranked.Contains(c.HeldOutShowId)) hits++;
        }

        return new ModelEvaluation(
            model,
            cases.Count,
            hits,
            cases.Count == 0 ? 0 : (double)hits / cases.Count,
            catalogueSize == 0 ? 0 : (double)recommendedAtLeastOnce.Count / catalogueSize);
    }

    /// <summary>
    /// Xếp hạng theo độ phổ biến: baseline để so. Nhiều người chọn hơn thì lên trước; hoà thì theo
    /// id để kết quả lặp lại được giữa các lần chạy.
    /// </summary>
    public static Func<int, IReadOnlyList<int>, IReadOnlyList<int>> PopularityRanker(
        IReadOnlyDictionary<int, int> interactionCountByShow)
        => (_, candidates) => candidates
            .OrderByDescending(id => interactionCountByShow.GetValueOrDefault(id))
            .ThenBy(id => id)
            .ToList();

    /// <summary>
    /// Xếp hạng theo mức hợp gu người nghe — chính công thức đang phục vụ người dùng thật qua
    /// <see cref="TasteMatcher"/>. Hoà thì theo id, cùng lý do như trên.
    /// </summary>
    public static Func<int, IReadOnlyList<int>, IReadOnlyList<int>> TasteRanker(
        IReadOnlyDictionary<int, TasteProfile> tasteByUser,
        IReadOnlyDictionary<int, ShowTags> tagsByShow)
        => (userId, candidates) =>
        {
            if (!tasteByUser.TryGetValue(userId, out var taste))
                return candidates.OrderBy(id => id).ToList();

            return candidates
                .OrderByDescending(id => tagsByShow.TryGetValue(id, out var tags)
                    ? TasteMatcher.RankingScore(taste, tags)
                    : 0f)
                .ThenBy(id => id)
                .ToList();
        };

    /// <summary>
    /// Chọn <paramref name="count"/> buổi diễn người này chưa từng chạm, để trộn cùng buổi bị giấu.
    ///
    /// Dùng bộ sinh ngẫu nhiên có hạt giống cố định: con số đưa vào báo cáo mà mỗi lần chạy lại ra
    /// một kiểu thì không ai kiểm chứng được, và cũng không so được hai lần đo với nhau.
    /// </summary>
    public static List<int> SampleNegatives(
        IReadOnlyList<int> catalogue,
        IReadOnlySet<int> alreadySeen,
        int count,
        Random random)
    {
        var pool = catalogue.Where(id => !alreadySeen.Contains(id)).ToList();

        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool.Take(count).ToList();
    }
}

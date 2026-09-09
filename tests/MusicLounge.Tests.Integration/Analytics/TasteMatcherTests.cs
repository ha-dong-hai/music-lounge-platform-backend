using FluentAssertions;
using MusicLounge.Application.Analytics.Common;

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-316. Công thức chấm điểm hợp gu, tách khỏi database.
///
/// Công thức này được ghi trong tài liệu đồ án nên nó không phải chi tiết cài đặt tự do đổi. Trước
/// đây nó nằm chôn bên trong dịch vụ ML.NET và chỉ chạy trong job nền mỗi 6 tiếng; giờ nó nuôi cả
/// đường tính trực tiếp trong request cho khách vãng lai và cho người dùng chưa bật đồng ý AI.
///
/// Hai đường dùng chung một bản cài đặt là có chủ đích: nếu tách đôi thì sớm muộn cùng một người sẽ
/// nhận hai thứ tự khác nhau tuỳ kết quả đến từ cache hay vừa được tính — đúng lớp lỗi mà codebase
/// này đã gặp nhiều lần.
/// </summary>
public sealed class TasteMatcherTests
{
    private static TasteProfile Likes(int[]? genres = null, int[]? moods = null,
        int[]? atmospheres = null, int[]? follows = null)
        => new((genres ?? []).ToHashSet(), (moods ?? []).ToHashSet(),
               (atmospheres ?? []).ToHashSet(), (follows ?? []).ToHashSet());

    private static ShowTags Show(int[]? genres = null, int[]? moods = null,
        int[]? atmospheres = null, int loungeId = 1)
        => new(ShowId: 1, loungeId, (genres ?? []).ToHashSet(),
               (moods ?? []).ToHashSet(), (atmospheres ?? []).ToHashSet());

    [Fact]
    public void APerfectMatchOnEveryDimension_ScoresOne()
    {
        // 0.4 + 0.4 + 0.2 = 1.0. Ghim luôn cả ba trọng số bằng một phép tính.
        var score = TasteMatcher.ContentScore(
            Likes(genres: [1], moods: [2], atmospheres: [3]),
            Show(genres: [1], moods: [2], atmospheres: [3]));

        score.Should().BeApproximately(1f, 0.0001f);
    }

    [Fact]
    public void GenreAndMoodWeighMoreThanAtmosphere()
    {
        // Thứ tự quan trọng của ba chiều là một quyết định sản phẩm: thể loại và tâm trạng nói lên
        // buổi diễn nghe như thế nào, còn không gian chỉ nói nó trông như thế nào.
        var genreOnly = TasteMatcher.ContentScore(Likes(genres: [1]), Show(genres: [1]));
        var atmosphereOnly = TasteMatcher.ContentScore(
            Likes(atmospheres: [1]), Show(atmospheres: [1]));

        genreOnly.Should().BeApproximately(TasteMatcher.GenreWeight, 0.0001f);
        atmosphereOnly.Should().BeApproximately(TasteMatcher.AtmosphereWeight, 0.0001f);
        genreOnly.Should().BeGreaterThan(atmosphereOnly);
    }

    [Fact]
    public void KnowingNothingAboutSomeoneScoresZero_NotSomethingMadeUp()
    {
        // Trường hợp phổ biến nhất trên một nền tảng mới. Phải ra 0 để đường gọi biết mà trả về
        // bảng thịnh hành, thay vì xếp theo một cái gu rỗng.
        TasteMatcher.ContentScore(Likes(), Show(genres: [1, 2], moods: [3]))
            .Should().Be(0f);
    }

    [Fact]
    public void AShowWithNoTagsScoresZero_EvenForSomeoneWithStrongTaste()
    {
        // Buổi diễn chưa gắn thẻ nào thì không có căn cứ để nói là hợp gu. Nó vẫn nằm trong danh
        // sách nhờ mức độ được quan tâm, chỉ là không được đẩy lên vì "hợp".
        TasteMatcher.ContentScore(Likes(genres: [1], moods: [2]), Show())
            .Should().Be(0f);
    }

    [Fact]
    public void PartialOverlapScoresBetweenNothingAndEverything()
    {
        // Người thích 2 thể loại, buổi diễn có 1 trong số đó cộng 1 thể loại khác: giao 1, hợp 3.
        var score = TasteMatcher.ContentScore(Likes(genres: [1, 2]), Show(genres: [2, 9]));

        score.Should().BeApproximately(TasteMatcher.GenreWeight / 3f, 0.0001f);
        score.Should().BeGreaterThan(0f).And.BeLessThan(TasteMatcher.GenreWeight);
    }

    [Fact]
    public void FollowingTheVenueAddsToTheScore_ItDoesNotReplaceIt()
    {
        // Thưởng theo dõi là cộng thêm SAU khi đã chấm hợp gu, không gộp vào công thức. Nhờ vậy một
        // buổi diễn ở phòng trà đang theo dõi nhưng lệch gu vẫn xếp dưới buổi đúng gu hoàn toàn.
        var followed = Show(genres: [9], loungeId: 7);

        var withFollow = TasteMatcher.RankingScore(
            Likes(genres: [1], follows: [7]), followed);
        var withoutFollow = TasteMatcher.RankingScore(
            Likes(genres: [1]), followed);

        withFollow.Should().BeApproximately(withoutFollow + TasteMatcher.FollowedVenueBoost, 0.0001f);
        withFollow.Should().BeLessThan(
            TasteMatcher.ContentScore(Likes(genres: [1]), Show(genres: [1])),
            "theo dõi phòng trà không đủ để vượt một buổi diễn khớp thể loại hoàn toàn");
    }

    [Fact]
    public void FollowingAnUnrelatedVenueChangesNothing()
    {
        var show = Show(genres: [1], loungeId: 7);

        TasteMatcher.RankingScore(Likes(genres: [1], follows: [99]), show)
            .Should().Be(TasteMatcher.ContentScore(Likes(genres: [1]), show));
    }
}

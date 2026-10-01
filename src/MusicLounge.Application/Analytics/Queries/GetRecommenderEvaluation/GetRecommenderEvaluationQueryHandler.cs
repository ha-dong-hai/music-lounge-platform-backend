using MediatR;
using MusicLounge.Application.Analytics.Common;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Analytics.Queries.GetRecommenderEvaluation;

/// <summary>
/// Chạy đánh giá offline cho hệ gợi ý và trả về kết quả so sánh với baseline.
///
/// Phương pháp và lý do chọn nó nằm ở <see cref="RecommenderEvaluation"/>. Việc của handler này là
/// dựng đúng đầu vào: lịch sử tương tác thật, tập ứng viên, và gu người nghe.
///
/// <b>Tương tác được tính là gì.</b> Vé đã mua và buổi diễn đã lưu vào danh sách quan tâm — đúng
/// hai tín hiệu mà bảng xếp hạng dùng, và cũng là hai tín hiệu tồn tại bất kể người dùng có bật
/// đồng ý AI hay không. Cố ý KHÔNG dùng nhật ký hành vi: nếu dùng, phép đo sẽ chỉ chạy được trên
/// nhóm nhỏ người đã bật đồng ý, và con số rút ra không đại diện cho ai cả.
/// </summary>
internal sealed class GetRecommenderEvaluationQueryHandler
    : IRequestHandler<GetRecommenderEvaluationQuery, RecommenderEvaluationDto>
{
    /// <summary>
    /// Số buổi diễn "nhiễu" trộn cùng buổi bị giấu. 99 + 1 = 100 ứng viên, đúng cách làm phổ biến
    /// trong tài liệu ngành — xếp hạng toàn bộ kho cho từng người vừa tốn kém vừa khiến con số của
    /// các hệ thống kho lớn nhỏ khác nhau không so được với nhau.
    /// </summary>
    private const int NegativeSamples = 99;

    /// <summary>
    /// Hạt giống cố định cho phép lấy mẫu. Con số đưa vào báo cáo mà mỗi lần chạy lại ra một kiểu
    /// thì không kiểm chứng được, và hai lần đo cũng không so được với nhau.
    /// </summary>
    private const int SamplingSeed = 20260909;

    private readonly IUnitOfWork _uow;
    private readonly ILoungeShowRepository _showRepo;

    public GetRecommenderEvaluationQueryHandler(IUnitOfWork uow, ILoungeShowRepository showRepo)
    {
        _uow = uow;
        _showRepo = showRepo;
    }

    public async Task<RecommenderEvaluationDto> Handle(
        GetRecommenderEvaluationQuery request, CancellationToken ct)
    {
        var k = Math.Clamp(request.K, 1, 50);

        // MLACP-520 (M-449): hai đoạn này hiện nguyên văn ở Admin > Tổng quan — viết bằng lời thường, thuật ngữ
        // kỹ thuật chỉ để trong ngoặc cho ai cần tra.
        const string method =
            "Cách đo: với mỗi người dùng, giấu đi lần tương tác gần nhất, trộn buổi diễn đó với 99 buổi " +
            "họ chưa từng xem, rồi kiểm tra mô hình có xếp được buổi bị giấu vào K vị trí đầu không " +
            "(tỉ lệ trúng HR@K, kiểu đo leave-one-out). Mốc so sánh: 'gợi ý những buổi nhiều người chọn nhất'.";

        const string caveat =
            "Phép đo trên dữ liệu cũ thiên vị buổi diễn phổ biến: một buổi có nhiều tương tác một phần là " +
            "vì chính hệ thống đã gợi ý nó cho nhiều người. Mốc 'nhiều người chọn nhất' vì thế được lợi " +
            "một cách giả tạo, nên mô hình cá nhân hoá chỉ ngang ngửa mốc này chưa chắc là kém — hãy " +
            "xem kèm độ phủ kho. Con số này cũng không thay được số đo trên người dùng thật (tỉ lệ bấm " +
            "vào gợi ý CTR, tỉ lệ mua vé), vốn mới cho biết gợi ý có thật sự hiệu quả.";

        // ── Lịch sử tương tác ─────────────────────────────────────────────────────────
        var tickets = await _uow.Repository<Ticket, Guid>().FindAsync(
            t => t.BuyerId != null
                && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used), ct);

        var saves = await _uow.Repository<ShowWishlist, int>().FindAsync(_ => true, ct);

        var interactions = tickets
            .Select(t => (UserId: t.BuyerId!.Value, ShowId: t.ShowId, At: t.CreatedAt))
            .Concat(saves.Select(w => (UserId: w.UserId, ShowId: w.LoungeShowId, At: w.CreatedAt)))
            .ToList();

        // ── Kho buổi diễn có thể gợi ý ────────────────────────────────────────────────
        var catalogue = (await _uow.Repository<LoungeShow, int>().FindAsync(
                s => s.Status == LoungeShowStatus.Published
                     || s.Status == LoungeShowStatus.Ongoing
                     || s.Status == LoungeShowStatus.Ended, ct))
            .Select(s => s.Id)
            .ToList();

        // ── Dựng các trường hợp kiểm tra ──────────────────────────────────────────────
        var random = new Random(SamplingSeed);
        var cases = new List<EvaluationCase>();

        foreach (var group in interactions.GroupBy(i => i.UserId))
        {
            var ordered = group.OrderBy(i => i.At).ToList();

            // Cần ít nhất hai tương tác: một cái để giấu đi làm đáp án, và ít nhất một cái còn lại
            // để mô hình có gì đó mà dựa vào.
            if (ordered.Count < 2) continue;

            var heldOut = ordered[^1];
            var seen = ordered.Select(i => i.ShowId).ToHashSet();

            if (!catalogue.Contains(heldOut.ShowId)) continue;

            var negatives = RecommenderEvaluation.SampleNegatives(
                catalogue, seen, NegativeSamples, random);

            // Không đủ buổi diễn khác để trộn thì bài kiểm tra trở nên vô nghĩa — mô hình chỉ phải
            // chọn giữa vài lựa chọn và gần như chắc chắn "đúng".
            if (negatives.Count < 5) continue;

            cases.Add(new EvaluationCase(
                heldOut.UserId, heldOut.ShowId, [.. negatives, heldOut.ShowId]));
        }

        if (cases.Count < RecommenderEvaluation.MinimumCases)
        {
            return new RecommenderEvaluationDto(
                Status: "NotEnoughHistory",
                Method: method,
                Caveat: $"Cần ít nhất {RecommenderEvaluation.MinimumCases} người dùng có từ hai " +
                        $"tương tác trở lên để phép đo có nghĩa; hiện có {cases.Count}. Dưới mức đó, " +
                        "mỗi người đúng hay sai làm điểm số nhảy hàng chục phần trăm, nên không có " +
                        "con số nào được đưa ra.",
                K: k,
                UsersWithEnoughHistory: cases.Count,
                CatalogueSize: catalogue.Count,
                Models: []);
        }

        // ── Hai mô hình đem so ────────────────────────────────────────────────────────
        var popularity = interactions
            .GroupBy(i => i.ShowId)
            .ToDictionary(g => g.Key, g => g.Count());

        var tasteByUser = await BuildTasteProfilesAsync(
            cases.Select(c => c.UserId).Distinct().ToList(), ct);

        var candidateShowIds = cases.SelectMany(c => c.CandidateShowIds).Distinct().ToList();
        var tagsByShow = (await _showRepo.GetShowTagsAsync(candidateShowIds, ct))
            .ToDictionary(t => t.ShowId);

        var results = new[]
        {
            RecommenderEvaluation.Evaluate(
                "popularity_baseline", cases,
                RecommenderEvaluation.PopularityRanker(popularity), k, catalogue.Count),

            RecommenderEvaluation.Evaluate(
                "content_based", cases,
                RecommenderEvaluation.TasteRanker(tasteByUser, tagsByShow), k, catalogue.Count)
        };

        return new RecommenderEvaluationDto(
            Status: "Evaluated",
            Method: method,
            Caveat: caveat,
            K: k,
            UsersWithEnoughHistory: cases.Count,
            CatalogueSize: catalogue.Count,
            Models: results.Select(r => new ModelEvaluationDto(
                    r.Model, r.Cases, r.Hits,
                    Math.Round((decimal)r.HitRateAtK * 100, 2),
                    Math.Round((decimal)r.CatalogueCoverage * 100, 2)))
                .ToList());
    }

    /// <summary>
    /// Gu của từng người: sở thích họ tự khai cộng phòng trà đang theo dõi — đúng thứ mô hình
    /// content-based dùng khi phục vụ người dùng thật, nên phép đo đo đúng cái đang chạy.
    /// </summary>
    private async Task<Dictionary<int, TasteProfile>> BuildTasteProfilesAsync(
        IReadOnlyList<int> userIds, CancellationToken ct)
    {
        var genres = await _uow.Repository<UserFavouriteGenre, int>()
            .FindAsync(g => userIds.Contains(g.UserId), ct);
        var moods = await _uow.Repository<UserFavouriteMood, int>()
            .FindAsync(m => userIds.Contains(m.UserId), ct);
        var atmospheres = await _uow.Repository<UserFavouriteAtmosphere, int>()
            .FindAsync(a => userIds.Contains(a.UserId), ct);
        var follows = await _uow.Repository<Follow, int>()
            .FindAsync(f => userIds.Contains(f.UserId), ct);

        var genreLookup = genres.ToLookup(g => g.UserId, g => g.GenreId);
        var moodLookup = moods.ToLookup(m => m.UserId, m => m.MoodId);
        var atmosphereLookup = atmospheres.ToLookup(a => a.UserId, a => a.AtmosphereId);
        var followLookup = follows.ToLookup(f => f.UserId, f => f.LoungeId);

        return userIds.ToDictionary(
            id => id,
            id => new TasteProfile(
                genreLookup[id].ToHashSet(),
                moodLookup[id].ToHashSet(),
                atmosphereLookup[id].ToHashSet(),
                followLookup[id].ToHashSet()));
    }
}

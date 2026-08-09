using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Trainers;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Services;

// Cong thuc dung theo dung tai lieu (khong tu suy dien them):
//   content_score = genre*0.4 + mood*0.4 + atmosphere*0.2  (Jaccard tung chieu)
//   collab_score  = ALS(user_event_scores matrix)          (ML.NET MatrixFactorizationTrainer)
//   custom_score  = Sum(match(event_custom_values, user_custom_preferences) * weight)
//   final_score   = content*0.5 + collab*0.3 + custom*0.2
//
// Registered AddScoped -> 1 instance/scope. RefreshRecommendationsJob resolve dich vu nay MOT LAN
// roi loop qua tung user trong cung 1 scope, nen model CF duoc train DUY NHAT 1 LAN moi lan chay
// job (khong retrain lai cho tung user) va tu dong "refit dinh ky" o lan chay tiep theo (scope moi).
internal sealed class MLNetRecommendationService : IAIRecommendationService
{
    // Additive boost applied AFTER the documented weighted formula (content*0.5 + collab*0.3 +
    // custom*0.2) is fully computed — not folded into it. DICE (a comparable live-music discovery
    // app) explicitly surfaces "artists/venues you follow" as its own signal distinct from taste-
    // matching; this system already has Follow (user follows a venue) sitting completely unused by
    // recommendations. Kept as a clearly separate post-hoc business-rule boost, not a change to the
    // core formula, since the formula's own comment states it follows a fixed spec exactly.
    private const float FollowedVenueBoost = 0.15f;

    private readonly ApplicationDbContext _ctx;
    private readonly IRepository<UserBehaviourLog, int> _logRepo;
    private readonly IRepository<UserFavouriteGenre, int> _genreRepo;
    private readonly IRepository<UserFavouriteMood, int> _moodRepo;
    private readonly IRepository<UserFavouriteAtmosphere, int> _atmosphereRepo;
    private readonly IRepository<Follow, int> _followRepo;
    private readonly ILoungeShowRepository _showRepo;
    private readonly IRepository<AiRecommendation, int> _recRepo;
    private readonly IUnitOfWork _uow;

    private bool _collabTrainingAttempted;
    private PredictionEngine<CfRow, CfPrediction>? _collabEngine;
    private HashSet<int> _trainedUserIds = [];
    private HashSet<int> _trainedShowIds = [];

    // Same "trained once per scope, reused across every user in RefreshRecommendationsJob's loop"
    // idea as _collabEngine above — GetTrendingAsync returns the same top-50 shows regardless of
    // which user is asking (city filter is always null here), so refetching it once per user in
    // the job's loop was pure repeated work with an identical result every time.
    private IReadOnlyList<Domain.Entities.LoungeShow>? _cachedTrendingShows;

    private async Task<IReadOnlyList<Domain.Entities.LoungeShow>> GetCachedTrendingShowsAsync(CancellationToken ct)
        => _cachedTrendingShows ??= await _showRepo.GetTrendingAsync(50, null, ct);

    public MLNetRecommendationService(
        ApplicationDbContext ctx,
        IRepository<UserBehaviourLog, int> logRepo,
        IRepository<UserFavouriteGenre, int> genreRepo,
        IRepository<UserFavouriteMood, int> moodRepo,
        IRepository<UserFavouriteAtmosphere, int> atmosphereRepo,
        IRepository<Follow, int> followRepo,
        ILoungeShowRepository showRepo,
        IRepository<AiRecommendation, int> recRepo,
        IUnitOfWork uow)
    {
        _ctx = ctx;
        _logRepo = logRepo;
        _genreRepo = genreRepo;
        _moodRepo = moodRepo;
        _atmosphereRepo = atmosphereRepo;
        _followRepo = followRepo;
        _showRepo = showRepo;
        _recRepo = recRepo;
        _uow = uow;
    }

    public async Task TriggerRecommendationRefreshAsync(int userId, CancellationToken ct = default)
    {
        var behaviourLogs = await _logRepo.FindAsync(l => l.UserId == userId, ct);
        var favouriteGenres = await _genreRepo.FindAsync(g => g.UserId == userId, ct);
        var favouriteMoods = await _moodRepo.FindAsync(m => m.UserId == userId, ct);
        var favouriteAtmospheres = await _atmosphereRepo.FindAsync(a => a.UserId == userId, ct);
        var followedLoungeIds = (await _followRepo.FindAsync(f => f.UserId == userId, ct))
            .Select(f => f.LoungeId).ToHashSet();

        var hasContentPrefs = favouriteGenres.Count > 0 || favouriteMoods.Count > 0 || favouriteAtmospheres.Count > 0;

        IReadOnlyList<AiRecommendation> recommendations;

        if (behaviourLogs.Count >= 5)
            recommendations = await ComputeHybridAsync(
                userId, favouriteGenres, favouriteMoods, favouriteAtmospheres, followedLoungeIds, ct);
        else if (hasContentPrefs || followedLoungeIds.Count > 0)
            recommendations = await ComputeContentBasedAsync(
                userId, favouriteGenres, favouriteMoods, favouriteAtmospheres, followedLoungeIds, ct);
        else
            return; // Stage 1 (Trending) handled at query time — no cache needed

        await PersistRecommendationsAsync(userId, recommendations, ct);
    }

    private async Task<IReadOnlyList<AiRecommendation>> ComputeContentBasedAsync(
        int userId,
        IReadOnlyList<UserFavouriteGenre> favouriteGenres,
        IReadOnlyList<UserFavouriteMood> favouriteMoods,
        IReadOnlyList<UserFavouriteAtmosphere> favouriteAtmospheres,
        IReadOnlySet<int> followedLoungeIds,
        CancellationToken ct)
    {
        var shows = await GetCachedTrendingShowsAsync(ct);
        var showById = shows.ToDictionary(s => s.Id);
        var contentScores = await ComputeContentScoresAsync(
            shows, favouriteGenres, favouriteMoods, favouriteAtmospheres, ct);

        return contentScores
            .Select(kvp =>
            {
                var isFollowedVenue = followedLoungeIds.Contains(showById[kvp.Key].LoungeId);
                var final = kvp.Value + (isFollowedVenue ? FollowedVenueBoost : 0f);
                return new AiRecommendation
                {
                    UserId = userId,
                    LoungeShowId = kvp.Key,
                    Algorithm = "content_based",
                    ContentScore = kvp.Value,
                    CollabScore = 0f,
                    CustomScore = 0f,
                    FinalScore = final,
                    Reason = isFollowedVenue
                        ? "Dựa trên thể loại/mood/không khí bạn yêu thích + venue bạn đang theo dõi"
                        : "Dựa trên thể loại/mood/không khí bạn yêu thích",
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(6)
                };
            })
            .Where(r => r.FinalScore > 0)
            .OrderByDescending(r => r.FinalScore)
            .Take(20)
            .ToList();
    }

    private async Task<IReadOnlyList<AiRecommendation>> ComputeHybridAsync(
        int userId,
        IReadOnlyList<UserFavouriteGenre> favouriteGenres,
        IReadOnlyList<UserFavouriteMood> favouriteMoods,
        IReadOnlyList<UserFavouriteAtmosphere> favouriteAtmospheres,
        IReadOnlySet<int> followedLoungeIds,
        CancellationToken ct)
    {
        var shows = await GetCachedTrendingShowsAsync(ct);
        var showIds = shows.Select(s => s.Id).ToList();
        var showById = shows.ToDictionary(s => s.Id);

        var contentScores = await ComputeContentScoresAsync(
            shows, favouriteGenres, favouriteMoods, favouriteAtmospheres, ct);
        var collabScores = await ComputeCollabScoresAsync(userId, showIds, ct);
        var customScores = await ComputeCustomScoresAsync(userId, showIds, ct);

        return showIds
            .Select(showId =>
            {
                var content = contentScores.GetValueOrDefault(showId, 0f);
                var collab = collabScores.GetValueOrDefault(showId, 0f);
                var custom = customScores.GetValueOrDefault(showId, 0f);
                var isFollowedVenue = followedLoungeIds.Contains(showById[showId].LoungeId);
                var final = content * 0.5f + collab * 0.3f + custom * 0.2f
                    + (isFollowedVenue ? FollowedVenueBoost : 0f);

                return new AiRecommendation
                {
                    UserId = userId,
                    LoungeShowId = showId,
                    Algorithm = "hybrid",
                    ContentScore = content,
                    CollabScore = collab,
                    CustomScore = custom,
                    FinalScore = final,
                    Reason = isFollowedVenue
                        ? "Hybrid: nội dung yêu thích + hành vi người dùng tương tự + tiêu chí riêng của venue + venue bạn đang theo dõi"
                        : "Hybrid: nội dung yêu thích + hành vi người dùng tương tự + tiêu chí riêng của venue",
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(6)
                };
            })
            .Where(r => r.FinalScore > 0)
            .OrderByDescending(r => r.FinalScore)
            .Take(20)
            .ToList();
    }

    // content_score = genre*0.4 + mood*0.4 + atmosphere*0.2 — moi chieu la Jaccard(so thich user, tag cua show).
    private async Task<Dictionary<int, float>> ComputeContentScoresAsync(
        IReadOnlyList<Domain.Entities.LoungeShow> shows,
        IReadOnlyList<UserFavouriteGenre> favouriteGenres,
        IReadOnlyList<UserFavouriteMood> favouriteMoods,
        IReadOnlyList<UserFavouriteAtmosphere> favouriteAtmospheres,
        CancellationToken ct)
    {
        var showIds = shows.Select(s => s.Id).ToList();

        var moodsByShow = (await _ctx.Set<LoungeShowMood>()
                .Where(m => showIds.Contains(m.LoungeShowId)).ToListAsync(ct))
            .GroupBy(m => m.LoungeShowId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.MoodId).ToHashSet());

        var atmospheresByShow = (await _ctx.Set<LoungeShowAtmosphere>()
                .Where(a => showIds.Contains(a.LoungeShowId)).ToListAsync(ct))
            .GroupBy(a => a.LoungeShowId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.AtmosphereId).ToHashSet());

        var userGenreIds = favouriteGenres.Select(g => g.GenreId).ToHashSet();
        var userMoodIds = favouriteMoods.Select(m => m.MoodId).ToHashSet();
        var userAtmosphereIds = favouriteAtmospheres.Select(a => a.AtmosphereId).ToHashSet();

        var result = new Dictionary<int, float>();
        foreach (var show in shows)
        {
            var showGenreIds = show.Genres.Select(g => g.GenreId).ToHashSet();
            var showMoodIds = moodsByShow.GetValueOrDefault(show.Id, []);
            var showAtmosphereIds = atmospheresByShow.GetValueOrDefault(show.Id, []);

            var genreScore = Jaccard(userGenreIds, showGenreIds);
            var moodScore = Jaccard(userMoodIds, showMoodIds);
            var atmosphereScore = Jaccard(userAtmosphereIds, showAtmosphereIds);

            result[show.Id] = genreScore * 0.4f + moodScore * 0.4f + atmosphereScore * 0.2f;
        }

        return result;
    }

    private static float Jaccard(HashSet<int> a, HashSet<int> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0f;
        var intersection = a.Intersect(b).Count();
        var union = a.Union(b).Count();
        return union == 0 ? 0f : (float)intersection / union;
    }

    // custom_score = Sum(match(event_custom_values, user_custom_preferences) * weight).
    // "match" = so sanh gia tri JSON dang chuoi (khong suy dien them logic fuzzy-match theo DataType
    // vi tai lieu khong mo ta chi tiet hon).
    private async Task<Dictionary<int, float>> ComputeCustomScoresAsync(
        int userId, IReadOnlyList<int> showIds, CancellationToken ct)
    {
        var result = showIds.ToDictionary(id => id, _ => 0f);

        var eventValues = await _ctx.Set<EventCustomValue>()
            .Where(v => showIds.Contains(v.ShowId)).ToListAsync(ct);
        if (eventValues.Count == 0) return result;

        var criteriaIds = eventValues.Select(v => v.CriteriaId).Distinct().ToList();
        var userPrefs = await _ctx.Set<UserCustomPreference>()
            .Where(p => p.UserId == userId && criteriaIds.Contains(p.CriteriaId)).ToListAsync(ct);
        if (userPrefs.Count == 0) return result;

        var prefByCriteria = userPrefs.ToDictionary(p => p.CriteriaId);

        foreach (var group in eventValues.GroupBy(v => v.ShowId))
        {
            var score = 0f;
            foreach (var value in group)
            {
                if (prefByCriteria.TryGetValue(value.CriteriaId, out var pref) &&
                    string.Equals(pref.Value, value.Value, StringComparison.Ordinal))
                    score += (float)pref.Weight;
            }
            result[group.Key] = score;
        }

        return result;
    }

    // collab_score = ALS(user_event_scores matrix). MatrixFactorizationTrainer cua ML.NET la mot
    // bien the ALS (alternating least squares) chuyen dung cho ma tran rating thua (sparse) —
    // dung API tieu chuan cua ML.NET (MapValueToKey + Recommendation().Trainers.MatrixFactorization)
    // thay vi tu code ALS tay, vi day la implementation da duoc kiem chung va toi uu san.
    private async Task<Dictionary<int, float>> ComputeCollabScoresAsync(
        int userId, IReadOnlyList<int> showIds, CancellationToken ct)
    {
        await EnsureCollabModelTrainedAsync(ct);

        var result = new Dictionary<int, float>();

        // Chua du du lieu de train, hoac user/show nay chua tung xuat hien trong ma tran train ->
        // collab_score = 0 (khong phai loi, chi la "chua co du lieu hanh vi de goi y kieu nay").
        if (_collabEngine is null || !_trainedUserIds.Contains(userId))
        {
            foreach (var id in showIds) result[id] = 0f;
            return result;
        }

        foreach (var showId in showIds)
        {
            if (!_trainedShowIds.Contains(showId))
            {
                result[showId] = 0f;
                continue;
            }

            var prediction = _collabEngine.Predict(new CfRow { UserId = (uint)userId, ShowId = (uint)showId });
            // Diem MF thô không bị chặn trong [0,1] — clamp để cộng bằng đơn vị với content/custom score.
            result[showId] = Math.Clamp(prediction.Score, 0f, 1f);
        }

        return result;
    }

    private async Task EnsureCollabModelTrainedAsync(CancellationToken ct)
    {
        if (_collabTrainingAttempted) return;
        _collabTrainingAttempted = true;

        var rows = await _ctx.Set<UserEventScore>()
            .Where(s => s.Score > 0)
            .Select(s => new CfRow { UserId = (uint)s.UserId, ShowId = (uint)s.ShowId, Label = (float)s.Score })
            .ToListAsync(ct);

        // Dataset qua nho de Matrix Factorization hoc duoc gi co y nghia (can it nhat vai chuc
        // rating trai deu tren nhieu user/show) — bo qua CF, chi dung content+custom score.
        if (rows.Count < 10) return;

        _trainedUserIds = rows.Select(r => (int)r.UserId).ToHashSet();
        _trainedShowIds = rows.Select(r => (int)r.ShowId).ToHashSet();

        var mlContext = new MLContext(seed: 0);
        var dataView = mlContext.Data.LoadFromEnumerable(rows);

        var options = new MatrixFactorizationTrainer.Options
        {
            MatrixColumnIndexColumnName = "UserIdEncoded",
            MatrixRowIndexColumnName = "ShowIdEncoded",
            LabelColumnName = nameof(CfRow.Label),
            NumberOfIterations = 20,
            ApproximationRank = 8, // rank thap phu hop dataset nho, tranh overfit
            LossFunction = MatrixFactorizationTrainer.LossFunctionType.SquareLossRegression
        };

        var pipeline = mlContext.Transforms.Conversion
            .MapValueToKey("UserIdEncoded", nameof(CfRow.UserId))
            .Append(mlContext.Transforms.Conversion.MapValueToKey("ShowIdEncoded", nameof(CfRow.ShowId)))
            .Append(mlContext.Recommendation().Trainers.MatrixFactorization(options));

        var model = pipeline.Fit(dataView);
        _collabEngine = mlContext.Model.CreatePredictionEngine<CfRow, CfPrediction>(model);
    }

    private async Task PersistRecommendationsAsync(
        int userId, IReadOnlyList<AiRecommendation> recommendations, CancellationToken ct)
    {
        var existing = await _recRepo.FindAsync(r => r.UserId == userId, ct);
        foreach (var old in existing)
            _recRepo.Remove(old);

        foreach (var rec in recommendations)
            _recRepo.Add(rec);

        await _uow.SaveChangesAsync(ct);
    }
}

// Model input/output rieng cho ML.NET pipeline — khong phai domain entity.
internal sealed class CfRow
{
    public uint UserId { get; set; }
    public uint ShowId { get; set; }
    public float Label { get; set; }
}

internal sealed class CfPrediction
{
    public float Score { get; set; }
}

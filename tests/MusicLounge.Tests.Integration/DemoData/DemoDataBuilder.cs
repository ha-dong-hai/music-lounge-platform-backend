using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.DemoData;

/// <summary>
/// MLACP-324. Dựng và dọn bộ dữ liệu trình diễn cho các nhánh AI.
///
/// Lớp này chỉ chứa phần việc, không chứa phần quyết định chạy hay không — quyết định đó nằm ở
/// <see cref="DemoDataScript"/>. Tách ra vì hai lý do: để bài kiểm tra chạy được đúng phần việc này
/// trên database của bộ test, và để phần bảo vệ khỏi chạy nhầm chỉ có đúng một chỗ.
///
/// <b>Sinh ĐẦU VÀO, không chế ĐẦU RA.</b> Dựng buổi diễn, sở thích tự khai, vé, lượt lưu quan tâm,
/// nhật ký hành vi — rồi chạy đúng job tính điểm của hệ thống. Điểm số là thứ hệ thống tự tính ra,
/// nên con số đưa vào báo cáo đứng vững khi bị hỏi. Viết thẳng dòng kết quả AI vào bảng thì lúc bảo
/// vệ không chứng minh được gì cả.
/// </summary>
internal sealed class DemoDataBuilder
{
    /// <summary>
    /// Dấu nhận biết. Mọi dòng dựng ra đều tìm lại được qua đúng hai mốc này, nên đường dọn không
    /// bao giờ phải đoán và không bao giờ chạm vào dữ liệu thật.
    /// </summary>
    public const string ShowPrefix = "[DEMO] ";
    public const string EmailDomain = "@demo.musiclounge.test";

    /// <summary>Mật khẩu chung của tài khoản demo. Chỉ dùng cho môi trường trình diễn.</summary>
    public const string DemoPassword = "MusicLoungeDemo2026!";

    private readonly ApplicationDbContext _db;
    private readonly Action<string> _log;

    public DemoDataBuilder(ApplicationDbContext db, Action<string> log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>
    /// Bốn nhóm người dùng, cố ý khác nhau để mỗi nhánh của hệ gợi ý đều có người để chỉ ra. Thiếu
    /// nhóm nào thì nhánh tương ứng không có gì để trình diễn lúc bảo vệ.
    /// </summary>
    private sealed record Persona(
        string Slug, string FullName, bool AiConsent, bool DeclaresTaste, int ClusterIndex);

    private static readonly Persona[] Personas =
    [
        // Đủ thông tin nhất: đã khai gu và đã cho phép phân tích hành vi. Đây là nhóm nuôi mô hình
        // lọc cộng tác — phải đủ đông thì mô hình mới chịu huấn luyện (ngưỡng 10 dòng điểm).
        new("an",    "Nguyen Thi Lan An",   true,  true,  0),
        new("binh",  "Tran Quoc Binh",      true,  true,  0),
        new("chi",   "Le Bao Chi",          true,  true,  1),
        new("dung",  "Pham Tien Dung",      true,  true,  1),
        new("giang", "Do Huong Giang",      true,  true,  2),
        new("hai",   "Vu Thanh Hai",        true,  true,  2),
        new("khanh", "Bui Gia Khanh",       true,  true,  3),
        new("linh",  "Ngo My Linh",         true,  true,  3),

        // Cold start của người dùng: chưa bao giờ khai gu, nhưng đã mua vé và đã lưu quan tâm. Hệ
        // thống phải suy được gu từ chính lịch sử đó (MLACP-321).
        new("minh",  "Dang Nhat Minh",      true,  false, 0),
        new("ngan",  "Hoang Kim Ngan",      true,  false, 2),

        // Ranh giới đồng ý: đã khai gu nhưng KHÔNG cho phân tích hành vi. Vẫn phải được cá nhân hoá
        // theo đúng thứ họ tự khai (MLACP-316) — nhóm này chứng minh ranh giới đặt đúng chỗ.
        new("phuc",  "Ly Hong Phuc",        false, true,  1),

        // Trắng hoàn toàn: mới đăng ký, chưa làm gì. Phải nhận bảng đang thịnh hành và nói rõ là
        // như vậy, không được giả vờ cá nhân hoá.
        new("quyen", "Trinh Dieu Quyen",    false, false, 3)
    ];

    private static readonly string[] ShowTitles =
    [
        "Dem nhac Trinh", "Bolero mot thuo", "Acoustic toi thu Sau", "Jazz va Ruou vang",
        "Indie Sai Gon", "Ballad mua cu", "Blues dem khuya", "Piano va Nen",
        "Dan ca duong dai", "Guitar moc", "Saxophone va Pho", "Dem nhac Ngo Thuy Mien"
    ];

    // ---------- sinh ----------

    public async Task<bool> SeedAsync(CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.Email.EndsWith(EmailDomain), ct))
        {
            _log("Đã có dữ liệu demo trong database này. Chạy Clean trước rồi hãy sinh lại.");
            return false;
        }

        var venues = await _db.Lounges
            .Where(l => l.Status == LoungeStatus.Approved)
            .OrderBy(l => l.Id)
            .ToListAsync(ct);

        var genres = await _db.Genres.OrderBy(g => g.Id).ToListAsync(ct);
        var moods = await _db.Moods.OrderBy(m => m.Id).ToListAsync(ct);
        var atmospheres = await _db.Atmospheres.OrderBy(a => a.Id).ToListAsync(ct);

        // Cố ý KHÔNG tự tạo phòng trà: một phòng trà kéo theo chủ sở hữu, gói đăng ký và tài khoản
        // nhận tiền. Dựng giả cả chuỗi đó là đi quá xa mục đích của một bộ dữ liệu trình diễn, và
        // là đúng loại dữ liệu giả mà sau này không ai dám xoá vì không rõ nó dính vào những gì.
        if (venues.Count == 0 || genres.Count < 2 || moods.Count == 0 || atmospheres.Count == 0)
        {
            _log($"Không đủ dữ liệu nền: {venues.Count} phòng trà đã duyệt, {genres.Count} thể loại, " +
                 $"{moods.Count} tâm trạng, {atmospheres.Count} không gian. " +
                 "Cần ít nhất 1 phòng trà đã duyệt và 2 thể loại.");
            return false;
        }

        var clusters = Math.Min(4, genres.Count);
        var now = DateTimeOffset.UtcNow;

        var shows = CreateShows(venues, now);
        await _db.SaveChangesAsync(ct);

        AddTags(shows, genres, moods, atmospheres, clusters);
        var ticketing = await CreateTicketingAsync(shows, now, ct);
        await _db.SaveChangesAsync(ct);

        var users = await CreateUsersAsync(genres, moods, atmospheres, clusters, ct);
        CreateInteractions(users, shows, ticketing, clusters, now);
        await _db.SaveChangesAsync(ct);

        // Chạy đúng job tính điểm của hệ thống thay vì tự viết dòng điểm.
        await new RecomputeUserEventScoresJob(_db).ExecuteAsync(new JobCancellationToken(false));

        var demoUserIds = users.Select(u => u.Id).ToList();
        var scored = await _db.Set<UserEventScore>().CountAsync(s => demoUserIds.Contains(s.UserId), ct);

        _log($"Đã sinh {shows.Count} buổi diễn và {users.Count} tài khoản.");
        _log($"Job tính điểm đã tạo {scored} dòng user_event_scores cho nhóm demo " +
             "(lọc cộng tác cần tổng ≥10 dòng thì mới huấn luyện).");
        _log($"Đăng nhập: demo.an{EmailDomain} … demo.quyen{EmailDomain}, mật khẩu {DemoPassword}");
        _log("Gợi ý tính sẵn do job refresh-recommendations sinh ra (chạy mỗi giờ), hoặc gọi " +
             "GET /api/v1/recommendations một lần để nó tự được xếp lịch.");
        return true;
    }

    /// <summary>
    /// Buổi diễn trải đều trên các phòng trà đang có, ngày diễn rải từ tuần sau tới hơn một tháng
    /// nữa để danh sách không dồn cục vào một mốc.
    /// </summary>
    private List<LoungeShow> CreateShows(IReadOnlyList<MusicLoungeVenue> venues, DateTimeOffset now)
    {
        var shows = new List<LoungeShow>();

        for (var i = 0; i < ShowTitles.Length; i++)
        {
            var start = now.AddDays(8 + i * 2.5);
            var show = new LoungeShow
            {
                LoungeId = venues[i % venues.Count].Id,
                Name = ShowPrefix + ShowTitles[i],
                Description =
                    "Buổi diễn dùng để trình diễn tính năng gợi ý. Dữ liệu do script MLACP-324 sinh ra.",
                Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Published,
                ScheduledStart = start,
                ScheduledEnd = start.AddHours(3)
            };
            _db.LoungeShows.Add(show);
            shows.Add(show);
        }

        return shows;
    }

    private void AddTags(
        IReadOnlyList<LoungeShow> shows, IReadOnlyList<MusicGenre> genres,
        IReadOnlyList<Mood> moods, IReadOnlyList<VenueAtmosphere> atmospheres, int clusters)
    {
        for (var i = 0; i < shows.Count; i++)
        {
            var cluster = i % clusters;

            _db.Add(new LoungeShowGenre { LoungeShowId = shows[i].Id, GenreId = genres[cluster].Id });
            _db.Add(new LoungeShowMood
            {
                LoungeShowId = shows[i].Id,
                MoodId = moods[cluster % moods.Count].Id
            });
            _db.Add(new LoungeShowAtmosphere
            {
                LoungeShowId = shows[i].Id,
                AtmosphereId = atmospheres[cluster % atmospheres.Count].Id
            });
        }
    }

    /// <summary>Mỗi buổi một hạng vé và một mức giá, đủ để gắn vé thật vào.</summary>
    private async Task<Dictionary<int, (int TierId, int PriceId)>> CreateTicketingAsync(
        IReadOnlyList<LoungeShow> shows, DateTimeOffset now, CancellationToken ct)
    {
        var tiers = shows.Select(s => new TicketTier
        {
            LoungeShowId = s.Id,
            Name = "Vé thường",
            AccessType = AccessType.Physical,
            TotalCapacity = 80
        }).ToList();

        _db.AddRange(tiers);
        await _db.SaveChangesAsync(ct);

        var prices = tiers.Select(t => new TicketPrice
        {
            TierId = t.Id,
            Name = "Giá chuẩn",
            Price = 250_000m,
            Quota = 80,
            IsActive = true,
            SaleStart = now.AddDays(-14),
            SaleEnd = now.AddDays(60),
            PurchaseChannel = PurchaseChannel.Online
        }).ToList();

        _db.AddRange(prices);
        await _db.SaveChangesAsync(ct);

        return shows
            .Select((s, i) => (ShowId: s.Id, tiers[i].Id, PriceId: prices[i].Id))
            .ToDictionary(x => x.ShowId, x => (x.Id, x.PriceId));
    }

    private async Task<List<User>> CreateUsersAsync(
        IReadOnlyList<MusicGenre> genres, IReadOnlyList<Mood> moods,
        IReadOnlyList<VenueAtmosphere> atmospheres, int clusters, CancellationToken ct)
    {
        // Cùng thuật toán băm với dịch vụ thật của hệ thống (ASP.NET Core Identity), nên tài khoản
        // demo đăng nhập được qua đúng đường đăng nhập bình thường.
        var hash = new PasswordHasher<User>().HashPassword(null!, DemoPassword);
        var now = DateTimeOffset.UtcNow;

        var users = Personas.Select(p => new User
        {
            Email = $"demo.{p.Slug}{EmailDomain}",
            FullName = p.FullName,
            Role = UserRole.Audience,
            AuthProvider = "local",
            PasswordHash = hash,
            EmailVerifiedAt = now,
            IsActive = true,
            AiConsent = p.AiConsent,
            TermsAcceptedAt = now
        }).ToList();

        _db.Users.AddRange(users);
        await _db.SaveChangesAsync(ct);

        for (var i = 0; i < Personas.Length; i++)
        {
            if (!Personas[i].DeclaresTaste) continue;

            var cluster = Personas[i].ClusterIndex % clusters;
            _db.Add(new UserFavouriteGenre { UserId = users[i].Id, GenreId = genres[cluster].Id });
            _db.Add(new UserFavouriteMood
            {
                UserId = users[i].Id,
                MoodId = moods[cluster % moods.Count].Id
            });
            _db.Add(new UserFavouriteAtmosphere
            {
                UserId = users[i].Id,
                AtmosphereId = atmospheres[cluster % atmospheres.Count].Id
            });
        }

        await _db.SaveChangesAsync(ct);
        return users;
    }

    /// <summary>
    /// Tương tác tập trung vào đúng cụm gu của từng người, rải trong hai tuần gần đây.
    ///
    /// Rải theo thời gian là có chủ ý: bảng thịnh hành tính theo suy giảm mũ với chu kỳ bán rã 72
    /// giờ, nên dồn hết vào một mốc thì mọi buổi diễn cùng điểm và bảng xếp hạng trông phẳng lì —
    /// đúng thứ cần tránh khi đi trình diễn.
    ///
    /// Mỗi người chạm ít nhất hai buổi diễn: đó là ngưỡng để họ có mặt trong ma trận huấn luyện của
    /// mô hình lọc cộng tác.
    /// </summary>
    private void CreateInteractions(
        IReadOnlyList<User> users, IReadOnlyList<LoungeShow> shows,
        IReadOnlyDictionary<int, (int TierId, int PriceId)> ticketing,
        int clusters, DateTimeOffset now)
    {
        for (var i = 0; i < Personas.Length; i++)
        {
            var persona = Personas[i];
            var user = users[i];

            // Tài khoản trắng thì phải trắng thật — nó tồn tại để chỉ ra hệ thống xử lý đúng khi
            // chưa biết gì về người dùng.
            if (!persona.DeclaresTaste && !persona.AiConsent) continue;

            var mine = shows.Where((_, idx) => idx % clusters == persona.ClusterIndex % clusters).ToList();
            if (mine.Count == 0) continue;

            // Mua vé buổi đầu trong cụm, lưu quan tâm hai buổi tiếp theo. Vé là tín hiệu mạnh nhất,
            // lưu quan tâm là tín hiệu ý định — hai loại khác nhau để bảng thịnh hành có cái mà
            // phân biệt trọng số.
            var bought = mine[0];
            var (tierId, priceId) = ticketing[bought.Id];

            _db.Add(new Ticket
            {
                // SQL Server sinh sẵn bằng NEWSEQUENTIALID(), nhưng đặt thẳng thì đúng ở mọi
                // provider và không phụ thuộc vào mặc định của cột.
                Id = Guid.NewGuid(),
                ShowId = bought.Id,
                TierId = tierId,
                PriceId = priceId,
                BuyerId = user.Id,
                Status = TicketStatus.Confirmed,
                QrCode = $"DEMO-{Guid.NewGuid():N}",
                PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = now.AddDays(-(i % 10) - 1)
            });

            foreach (var (show, offset) in mine.Skip(1).Take(2).Select((s, k) => (s, k)))
            {
                _db.Add(new ShowWishlist
                {
                    UserId = user.Id,
                    LoungeShowId = show.Id,
                    CreatedAt = now.AddDays(-(i % 7)).AddHours(-offset * 6)
                });
            }

            // Nhật ký hành vi chỉ ghi cho người đã đồng ý — đúng như job ghi nhật ký của hệ thống
            // làm. Sinh cho cả người chưa đồng ý là dựng ra một tình trạng không thể xảy ra thật,
            // và sẽ khiến bộ dữ liệu này nói dối về chính ranh giới đồng ý mà nó đi trình diễn.
            if (!persona.AiConsent) continue;

            foreach (var (show, offset) in mine.Take(3).Select((s, k) => (s, k)))
            {
                _db.Add(new UserBehaviourLog
                {
                    UserId = user.Id,
                    LoungeShowId = show.Id,
                    Action = offset == 0 ? BehaviourAction.ViewEventLong : BehaviourAction.ViewEvent,
                    DurationSeconds = offset == 0 ? 180 : 40,
                    CreatedAt = now.AddDays(-(i % 12)).AddHours(-offset * 3)
                });
            }
        }
    }

    // ---------- dọn ----------

    /// <summary>
    /// Xoá theo đúng chiều khoá ngoại, từ dòng phụ thuộc ngược lên gốc. Mọi thứ đều tìm lại được qua
    /// hai dấu nhận biết, nên không sót dòng nào và không chạm vào dữ liệu thật.
    /// </summary>
    public async Task<(int Shows, int Users)> CleanAsync(CancellationToken ct = default)
    {
        var userIds = await _db.Users
            .Where(u => u.Email.EndsWith(EmailDomain))
            .Select(u => u.Id)
            .ToListAsync(ct);

        var showIds = await _db.LoungeShows
            .Where(s => s.Name.StartsWith(ShowPrefix))
            .Select(s => s.Id)
            .ToListAsync(ct);

        if (userIds.Count == 0 && showIds.Count == 0)
        {
            _log("Không tìm thấy dữ liệu demo nào để xoá.");
            return (0, 0);
        }

        // Lấy khoá con trước rồi xoá theo khoá, thay vì viết điều kiện đi qua quan hệ: câu lệnh xoá
        // hàng loạt không dịch được phép nối bảng trên mọi provider.
        var tierIds = await _db.Set<TicketTier>()
            .Where(t => showIds.Contains(t.LoungeShowId))
            .Select(t => t.Id)
            .ToListAsync(ct);

        await _db.Set<AiRecommendation>()
            .Where(r => userIds.Contains(r.UserId) || showIds.Contains(r.LoungeShowId))
            .ExecuteDeleteAsync(ct);

        await _db.Set<UserEventScore>()
            .Where(s => userIds.Contains(s.UserId) || showIds.Contains(s.ShowId))
            .ExecuteDeleteAsync(ct);

        await _db.BehaviourLogs
            .Where(b => userIds.Contains(b.UserId) || showIds.Contains(b.LoungeShowId))
            .ExecuteDeleteAsync(ct);

        await _db.Wishlists
            .Where(w => userIds.Contains(w.UserId) || showIds.Contains(w.LoungeShowId))
            .ExecuteDeleteAsync(ct);

        await _db.Tickets
            .Where(t => showIds.Contains(t.ShowId)
                || (t.BuyerId != null && userIds.Contains(t.BuyerId.Value)))
            .ExecuteDeleteAsync(ct);

        await _db.Set<UserFavouriteGenre>().Where(g => userIds.Contains(g.UserId)).ExecuteDeleteAsync(ct);
        await _db.Set<UserFavouriteMood>().Where(m => userIds.Contains(m.UserId)).ExecuteDeleteAsync(ct);
        await _db.Set<UserFavouriteAtmosphere>().Where(a => userIds.Contains(a.UserId)).ExecuteDeleteAsync(ct);

        await _db.Set<LoungeShowGenre>().Where(g => showIds.Contains(g.LoungeShowId)).ExecuteDeleteAsync(ct);
        await _db.Set<LoungeShowMood>().Where(m => showIds.Contains(m.LoungeShowId)).ExecuteDeleteAsync(ct);
        await _db.Set<LoungeShowAtmosphere>().Where(a => showIds.Contains(a.LoungeShowId)).ExecuteDeleteAsync(ct);

        await _db.Set<TicketPrice>().Where(p => tierIds.Contains(p.TierId)).ExecuteDeleteAsync(ct);
        await _db.Set<TicketTier>().Where(t => tierIds.Contains(t.Id)).ExecuteDeleteAsync(ct);

        await _db.LoungeShows.Where(s => showIds.Contains(s.Id)).ExecuteDeleteAsync(ct);
        await _db.Users.Where(u => userIds.Contains(u.Id)).ExecuteDeleteAsync(ct);

        _log($"Đã xoá {showIds.Count} buổi diễn và {userIds.Count} tài khoản demo.");
        return (showIds.Count, userIds.Count);
    }
}

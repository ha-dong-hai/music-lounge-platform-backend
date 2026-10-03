using System.Net.Http.Json;
using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.DemoData;

/// <summary>
/// MLACP-577. Bộ DỮ LIỆU MẪU đa dạng cho cả sàn — để thử gợi ý theo gu, học máy, thịnh hành, đánh giá, tài chính và
/// quyết toán trên dữ liệu đủ dày. Đo trên Azure 03/10/2026: 2 buổi sắp diễn (đều Bolero), 1 đánh giá, 5/12 thể loại
/// chưa có buổi nào → đổi sở thích không thể thấy gợi ý đổi; mọi ngưỡng học máy đều chưa chạm.
///
/// <b>Khác <see cref="DemoDataBuilder"/> (MLACP-324, chỉ phục vụ nhánh AI) ở ba điểm chủ dự án yêu cầu:</b>
/// 1. Tên như thật, KHÔNG có tiền tố "[DEMO] ". Dấu nhận biết để dọn nằm ở chỗ người xem không thấy: buổi diễn có
///    <c>CreatedBy</c> = một tài khoản đánh dấu; tài khoản khán giả có email đuôi <see cref="EmailDomain"/>; nghệ sĩ có
///    <c>CreatedByUserId</c> = tài khoản đánh dấu.
/// 2. Vé đi kèm THANH TOÁN + SỔ CÁI + LỊCH QUYẾT TOÁN, do chính hệ thống tính: vé được mua qua đúng API (giữ chỗ →
///    thanh toán → callback VNPay giả) — xem <see cref="SampleDataHost"/>.
/// 3. Có BUỔI ĐÃ DIỄN và ĐÁNH GIÁ: buổi được dựng ở tương lai, bán vé, soát vé, rồi lùi ngày và kết thúc bằng đúng hàm
///    chuyển trạng thái của hệ thống (<see cref="LoungeShowLifecycle.TryMarkEnded"/> — hàm mà AutoEndStaleShowsJob gọi),
///    nên cửa sổ đánh giá mở đúng luật; đánh giá gửi qua đúng API.
///
/// <b>Sinh ĐẦU VÀO, không chế ĐẦU RA</b> — nguyên tắc của bộ cũ được giữ: điểm số, tiền, trạng thái là do hệ thống tính.
/// Hai chỗ bộ dựng ghi thẳng vào bảng, và vì sao:
/// - NGÀY GIỜ: hệ thống luôn ghi "bây giờ". Một bộ dữ liệu mà mọi vé đều mua trong cùng một phút thì bảng thịnh hành
///   (bán rã 72 giờ), dự báo tốc độ bán vé và lịch sử tài chính đều phẳng lì. Bộ dựng dời CẢ CỤM dòng của một lần mua
///   (thanh toán, vé, sổ cái, quyết toán) về cùng một mốc — không sửa một con số tiền nào.
/// - SOÁT VÉ và NHẬT KÝ HÀNH VI: soát vé thật cần nhân viên quét mã QR; nhật ký hành vi do job nền ghi mà máy chủ dựng
///   không chạy job nền. Cả hai được ghi thẳng, đúng hình dạng hệ thống ghi.
///
/// TRẦN: quy mô "vừa" (khoảng 40 buổi sắp diễn, 15 buổi đã diễn, 40 khán giả). Số vé mỗi buổi còn bị chặn bởi gói đăng ký
/// của phòng trà (HoldTicket) — lần mua bị từ chối thì được đếm và bỏ qua, không làm hỏng cả lượt dựng.
/// </summary>
internal sealed class SampleDataBuilder
{
    public const string EmailDomain = "@mau.musiclounge.test";
    public const string MarkerEmail = "danh-dau" + EmailDomain;
    /// <summary>Mật khẩu chung của tài khoản khán giả mẫu. Chỉ dùng cho môi trường thử.</summary>
    public const string SamplePassword = "MusicLoungeMau2026!";

    private const int UpcomingShows = 40;
    private const int PastShows = 15;
    private const int Audience = 40;

    private readonly ISampleHost _host;
    private readonly Action<string> _log;
    private readonly Random _rng = new(577); // cố định hạt: hai lần dựng cho cùng một bộ dữ liệu

    public SampleDataBuilder(ISampleHost host, Action<string> log)
    {
        _host = host;
        _log = log;
    }

    // ---------- nội dung ----------

    /// <summary>Sáu cụm gu. Mỗi cụm là tên các thể loại (khớp theo tên với danh mục của database đích; thể loại nào
    /// database không có thì bỏ qua — danh mục do Admin quản lý, bộ dựng không tự thêm).</summary>
    private static readonly string[][] Clusters =
    [
        ["Bolero", "Nhạc trữ tình"],
        ["Nhạc jazz", "Blues/Jazz"],
        ["Acoustic", "Indie", "Folk"],
        ["Nhạc trẻ", "R&B"],
        ["Rock", "EDM"],
        ["Nhạc cổ điển"],
    ];

    private static readonly string[][] TitlesByCluster =
    [
        ["Tình khúc vượt thời gian", "Đêm bolero phố cũ", "Chuyện tình lan và điệp", "Mưa trên phố Huế", "Sầu tím thiệp hồng", "Những chiều không nắng", "Thư tình cuối mùa thu", "Hàn Mặc Tử và những khúc buồn", "Đêm nhạc xưa bên hiên", "Căn nhà ngoại ô"],
        ["Jazz sau cơn mưa", "Blue hour quartet", "Saxophone và phố khuya", "Đêm swing Sài Gòn", "Một tách jazz nóng", "Standards cho người ở lại", "Bossa nova chiều thứ Bảy", "Round midnight", "Tam tấu piano đêm muộn"],
        ["Mộc — guitar và giọng hát", "Những bài hát viết ở Đà Lạt", "Đêm indie của người trẻ", "Hát cho nhau nghe", "Dân ca qua cây đàn guitar", "Chuyện kể bên lửa trại", "Bản nháp số 7", "Folk và những chuyến đi", "Phòng thu mở"],
        ["Thanh xuân của chúng ta", "R&B đêm thứ Sáu", "Những bản hit một thời", "Đêm nhạc trẻ unplugged", "Soul và phố", "Playlist của tuổi hai mươi", "Chill cuối tuần", "Ngày mai người ta lấy chồng — acoustic cover"],
        ["Rock Việt một thời", "Đêm guitar điện", "Unplugged rock ballad", "Electronic live set", "Bức tường và những bài hát cũ", "Synth và ánh đèn", "Đêm nhạc rock sinh viên"],
        ["Dạ khúc cho dương cầm", "Tứ tấu dây — Mùa thu", "Chopin và Debussy", "Đêm nhạc thính phòng", "Cello độc tấu", "Từ Bach tới Piazzolla"],
    ];

    private static readonly string[] PerformerNames =
    [
        "Thu Hà", "Minh Khôi", "Bảo Trâm", "Quốc Thiên An", "Hồng Vy", "Đăng Khoa Trio", "Lam Phương Quartet", "Mộc Miên",
        "Tuấn Kiệt", "Hạ Vân", "Nhóm Phố Cũ", "Ngọc Diệp", "Trung Hiếu Saxophone", "Khánh Linh", "Ban nhạc Ngược Gió", "Thiên Di",
        "Hoài Nam", "Diễm Quỳnh", "Tứ tấu Sông Hàn", "Gia Bảo", "Mai Chi", "Nhóm Lửa Trại", "An Nhiên", "Đức Minh Piano",
    ];

    private static readonly string[] Ho = ["Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý"];
    private static readonly string[] Dem = ["Thị", "Văn", "Minh", "Ngọc", "Thanh", "Quốc", "Hoài", "Bảo", "Gia", "Thu", "Đức", "Kim"];
    private static readonly string[] Ten = ["An", "Bình", "Chi", "Dung", "Giang", "Hải", "Khánh", "Linh", "Minh", "Ngân", "Oanh", "Phúc", "Quyên", "Sơn", "Trang", "Uyên", "Vy", "Yến", "Hưng", "Thảo"];

    /// <summary>Lời bình theo số sao. Viết như người thật viết: dài ngắn khác nhau, có lời chê cụ thể, có xuống dòng.
    /// Không có lời thô tục — bộ dữ liệu mẫu không nên là thứ làm đầy hàng đợi kiểm duyệt.</summary>
    private static readonly Dictionary<int, string[]> Comments = new()
    {
        [5] =
        [
            "Rất hay!", "Giọng ca mộc mà sâu, ban nhạc chơi rất chắc tay. Sẽ quay lại.",
            "Ngồi bàn sát sân khấu, nghe rõ từng hơi thở của ca sĩ. Đáng từng đồng.",
            "Đi một mình mà không thấy lạc lõng. Không gian ấm, nhân viên để ý từng bàn.",
            "Lần đầu nghe nhạc sống ở phòng trà, không ngờ khác xa nghe qua loa đến vậy.\nBài cuối cả phòng hát theo, nổi da gà.",
            "Âm thanh cân, không bị chói. Ca sĩ giao lưu rất duyên, kể chuyện về từng bài hát trước khi hát. Mình đưa ba mẹ đi cùng, hai ông bà vui cả tuần. Giá vé so với chất lượng là quá hợp lý, chỉ tiếc là đêm diễn hơi ngắn — mong phòng trà mở thêm suất.",
            "Tuyệt vời, 10 điểm.", "Đáng tiền. Lần sau sẽ rủ thêm bạn.",
        ],
        [4] =
        [
            "Hay, nhưng bắt đầu trễ khoảng 15 phút.", "Nhạc hay, đồ uống hơi đắt so với mặt bằng chung.",
            "Ca sĩ hát tốt, chỉ có điều bàn phía sau hơi khuất tầm nhìn.",
            "Không gian đẹp, âm thanh ổn. Trừ một sao vì chỗ gửi xe hơi xa.",
            "Đêm diễn chỉn chu. Phần giữa hơi trầm nhưng nửa sau rất cuốn.", "Ổn áp, sẽ quay lại nếu có chương trình khác.",
        ],
        [3] =
        [
            "Tạm được.", "Nhạc ổn nhưng phòng hơi đông, khó nói chuyện.",
            "Ca sĩ chính hát hay, khách mời thì chưa tới. Phục vụ chậm lúc cao điểm.",
            "Bình thường, không có gì đặc biệt so với giá vé.",
        ],
        [2] =
        [
            "Âm thanh hơi rè ở nửa đầu, sau đó mới ổn.", "Bắt đầu trễ gần nửa tiếng mà không ai thông báo.",
            "Chỗ ngồi chật, máy lạnh yếu. Nhạc thì được.",
        ],
        [1] =
        [
            "Thất vọng. Đặt bàn gần sân khấu nhưng tới nơi bị xếp ra phía sau.", "Âm thanh tệ, bắt đầu trễ 40 phút, không một lời xin lỗi.",
        ],
    };

    // ---------- sinh ----------

    public async Task<bool> SeedAsync(CancellationToken ct = default)
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (await db.Users.AnyAsync(u => u.Email == MarkerEmail, ct))
        {
            _log("Database này đã có dữ liệu mẫu. Chạy Clean trước rồi hãy sinh lại.");
            return false;
        }

        var venues = await db.Lounges.Where(l => VenueLifecycle.Operating.Contains(l.Status)).OrderBy(l => l.Id).ToListAsync(ct);
        var genres = await db.Genres.ToListAsync(ct);
        var moods = await db.Moods.OrderBy(m => m.Id).ToListAsync(ct);
        var atmospheres = await db.Atmospheres.OrderBy(a => a.Id).ToListAsync(ct);
        if (venues.Count == 0 || genres.Count < 2 || moods.Count == 0 || atmospheres.Count == 0)
        {
            _log($"Không đủ dữ liệu nền: {venues.Count} phòng trà đang hoạt động, {genres.Count} thể loại, " +
                 $"{moods.Count} tâm trạng, {atmospheres.Count} không gian. Cần ít nhất 1 phòng trà và 2 thể loại.");
            return false;
        }

        // Cụm gu → thể loại THẬT của database này. Cụm không khớp thể loại nào thì mượn thể loại theo vòng, để vẫn đủ 6 cụm.
        var genreByName = genres.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);
        var clusterGenres = Clusters
            .Select((names, i) =>
            {
                var khop = names.Where(genreByName.ContainsKey).Select(n => genreByName[n]).ToList();
                return khop.Count > 0 ? khop : [genres[i % genres.Count]];
            })
            .ToList();

        var now = DateTimeOffset.UtcNow;
        var marker = await CreateMarkerAsync(db, now, ct);
        var performers = await CreatePerformersAsync(db, marker.Id, ct);
        var users = await CreateAudienceAsync(db, clusterGenres, moods, atmospheres, now, ct);
        var shows = await CreateShowsAsync(db, venues, clusterGenres, moods, atmospheres, performers, marker.Id, now, ct);

        var upcoming = shows.Where(s => !s.Past).ToList();
        var past = shows.Where(s => s.Past).ToList();

        var stats = new Stats();
        await FollowAndWishlistAsync(users, venues, upcoming, stats);
        await SellAsync(db, users, shows, stats, ct);
        await WriteBehaviourLogsAsync(db, users, shows, now, ct);
        await FinishPastShowsAsync(scope.ServiceProvider, db, users, past, stats, now, ct);
        await SpreadUpcomingPurchasesAsync(db, upcoming, now, ct);

        // Chạy đúng job tính điểm của hệ thống thay vì tự viết dòng điểm (giữ nguyên cách của bộ MLACP-324).
        await new RecomputeUserEventScoresJob(db).ExecuteAsync(new JobCancellationToken(false));

        var userIds = users.Select(u => u.User.Id).ToList();
        var scored = await db.Set<UserEventScore>().CountAsync(s => userIds.Contains(s.UserId), ct);
        var lech = await UnbalancedJournalsAsync(db, shows.Select(s => s.Show.Id).ToList(), ct);

        _log($"Phòng trà dùng: {venues.Count}. Buổi sắp diễn: {upcoming.Count}. Buổi đã diễn: {past.Count}. Khán giả: {users.Count}. Nghệ sĩ: {performers.Count}.");
        _log($"Mua vé qua API: {stats.PurchasesOk} lần thành công ({stats.Tickets} vé), {stats.PurchasesRefused} lần bị hệ thống từ chối (hết chỗ / trần gói đăng ký — bỏ qua).");
        _log($"Theo dõi phòng trà: {stats.Follows}. Lưu quan tâm: {stats.Wishlists}. Đánh giá qua API: {stats.Ratings} (bị từ chối: {stats.RatingsRefused}).");
        _log($"Job tính điểm đã tạo {scored} dòng user_event_scores cho nhóm mẫu.");
        _log(lech == 0
            ? "Đối soát sổ cái: mọi bút toán của vé mẫu đều cân (nợ = có)."
            : $"CẢNH BÁO: {lech} bút toán của vé mẫu KHÔNG cân — không dùng bộ dữ liệu này cho báo cáo tài chính.");
        _log($"Đăng nhập khán giả mẫu: khangia01{EmailDomain} … khangia{Audience:00}{EmailDomain}, mật khẩu {SamplePassword}");
        return true;
    }

    private sealed class Stats
    {
        public int PurchasesOk, PurchasesRefused, Tickets, Follows, Wishlists, Ratings, RatingsRefused;
    }

    private sealed record Person(User User, int Cluster, bool AiConsent, bool DeclaresTaste, bool Blank);
    private sealed record Staged(LoungeShow Show, int Cluster, bool Past, List<Guid> PriceIds, int Quality, int DaysAgo);
    private sealed record Purchase(Guid PaymentId, List<Guid> TicketIds, Guid BuyerId);

    private static async Task<User> CreateMarkerAsync(ApplicationDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        // Tài khoản đánh dấu: không đăng nhập được (IsActive = false, mật khẩu ngẫu nhiên không ai biết). Nó chỉ tồn tại để
        // mọi dòng dữ liệu mẫu trỏ về, cho đường dọn tìm lại được mà không phải đoán.
        var marker = new User
        {
            Email = MarkerEmail,
            FullName = "Dữ liệu mẫu MLACP-577",
            Role = UserRole.Audience,
            AuthProvider = "local",
            PasswordHash = new PasswordHasher<User>().HashPassword(null!, Guid.NewGuid().ToString("N")),
            IsActive = false,
            TermsAcceptedAt = now
        };
        db.Users.Add(marker);
        await db.SaveChangesAsync(ct);
        return marker;
    }

    private static async Task<List<Performer>> CreatePerformersAsync(ApplicationDbContext db, Guid markerId, CancellationToken ct)
    {
        var list = PerformerNames.Select(n => new Performer
        {
            Name = n,
            Type = n.Contains("Trio") || n.Contains("Quartet") || n.Contains("Nhóm") || n.Contains("Ban nhạc") || n.Contains("Tứ tấu")
                ? PerformerType.Band : PerformerType.Solo,
            CreatedByUserId = markerId
        }).ToList();
        db.AddRange(list);
        await db.SaveChangesAsync(ct);
        return list;
    }

    private async Task<List<Person>> CreateAudienceAsync(
        ApplicationDbContext db, IReadOnlyList<List<MusicGenre>> clusterGenres,
        IReadOnlyList<Mood> moods, IReadOnlyList<VenueAtmosphere> atmospheres, DateTimeOffset now, CancellationToken ct)
    {
        // Cùng thuật toán băm với dịch vụ thật (ASP.NET Core Identity) → đăng nhập được qua đường đăng nhập bình thường.
        var hash = new PasswordHasher<User>().HashPassword(null!, SamplePassword);
        var people = new List<Person>();

        for (var i = 0; i < Audience; i++)
        {
            // Ba tài khoản TRẮNG (mới đăng ký, chưa làm gì): hệ thống phải nói rõ là đang đưa bảng thịnh hành.
            var blank = i >= Audience - 3;
            // Khoảng 1/4 không cho phân tích hành vi (ranh giới đồng ý), khoảng 1/5 chưa khai gu (cold start từ lịch sử).
            var consent = !blank && i % 4 != 3;
            var declares = !blank && i % 5 != 4;
            var user = new User
            {
                Email = $"khangia{i + 1:00}{EmailDomain}",
                FullName = $"{Ho[i % Ho.Length]} {Dem[(i * 7) % Dem.Length]} {Ten[(i * 3) % Ten.Length]}",
                Role = UserRole.Audience,
                AuthProvider = "local",
                PasswordHash = hash,
                EmailVerifiedAt = now,
                IsActive = true,
                AiConsent = consent,
                TermsAcceptedAt = now
            };
            people.Add(new Person(user, i % clusterGenres.Count, consent, declares, blank));
        }

        db.Users.AddRange(people.Select(p => p.User));
        await db.SaveChangesAsync(ct);

        foreach (var p in people.Where(p => p.DeclaresTaste))
        {
            foreach (var g in clusterGenres[p.Cluster])
                db.Add(new UserFavouriteGenre { UserId = p.User.Id, GenreId = g.Id });
            db.Add(new UserFavouriteMood { UserId = p.User.Id, MoodId = moods[p.Cluster % moods.Count].Id });
            db.Add(new UserFavouriteAtmosphere { UserId = p.User.Id, AtmosphereId = atmospheres[p.Cluster % atmospheres.Count].Id });
        }

        await db.SaveChangesAsync(ct);
        return people;
    }

    private async Task<List<Staged>> CreateShowsAsync(
        ApplicationDbContext db, IReadOnlyList<MusicLoungeVenue> venues, IReadOnlyList<List<MusicGenre>> clusterGenres,
        IReadOnlyList<Mood> moods, IReadOnlyList<VenueAtmosphere> atmospheres, IReadOnlyList<Performer> performers,
        Guid markerId, DateTimeOffset now, CancellationToken ct)
    {
        var staged = new List<Staged>();
        var dem = new int[clusterGenres.Count];

        for (var i = 0; i < UpcomingShows + PastShows; i++)
        {
            var pastShow = i >= UpcomingShows;
            var cluster = i % clusterGenres.Count;
            var titles = TitlesByCluster[cluster];
            var title = titles[dem[cluster]++ % titles.Length];
            var venue = venues[(i * 3 + cluster) % venues.Count];

            // Sắp diễn: rải từ HÔM NAY tới 6 tuần, giờ diễn buổi tối theo giờ Việt Nam (19:30–21:00 = 12:30–14:00 UTC).
            // Đã diễn: dựng tạm ở 2 ngày tới để còn bán được vé; FinishPastShowsAsync sẽ lùi về quá khứ.
            var ngay = pastShow ? 2 : (int)Math.Round(i * 42.0 / UpcomingShows);
            var start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(ngay).AddHours(12.5 + (i % 4) * 0.5);
            if (start <= now.AddHours(1)) start = start.AddDays(1); // suất tối nay đã qua giờ thì đẩy sang mai

            var show = new LoungeShow
            {
                LoungeId = venue.Id,
                // Tên trùng trong cùng cụm (hai đêm cùng chương trình) thì thêm số đêm — như phòng trà thật vẫn làm.
                Name = dem[cluster] > titles.Length ? $"{title} — đêm {1 + (dem[cluster] - 1) / titles.Length}" : title,
                Description = $"Một đêm {clusterGenres[cluster][0].Name.ToLowerInvariant()} tại {venue.Name}. Cửa mở trước giờ diễn 30 phút; vui lòng tới sớm để nhận chỗ.",
                Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Published,
                ScheduledStart = start,
                ScheduledEnd = start.AddHours(2.5)
            };
            db.LoungeShows.Add(show);
            staged.Add(new Staged(show, cluster, pastShow, [], Quality: 3 + (i * 7) % 3 - (i % 11 == 0 ? 2 : 0),
                DaysAgo: pastShow ? 3 + (i - UpcomingShows) * 4 : 0));
        }

        await db.SaveChangesAsync(ct);

        for (var i = 0; i < staged.Count; i++)
        {
            var (show, cluster) = (staged[i].Show, staged[i].Cluster);
            var gs = clusterGenres[cluster];
            db.Add(new LoungeShowGenre { LoungeShowId = show.Id, GenreId = gs[i % gs.Count].Id });
            if (gs.Count > 1 && i % 3 == 0)
                db.Add(new LoungeShowGenre { LoungeShowId = show.Id, GenreId = gs[(i + 1) % gs.Count].Id });
            db.Add(new LoungeShowMood { LoungeShowId = show.Id, MoodId = moods[(cluster + i / 7) % moods.Count].Id });
            db.Add(new LoungeShowAtmosphere { LoungeShowId = show.Id, AtmosphereId = atmospheres[cluster % atmospheres.Count].Id });

            var soNgheSi = 1 + i % 3;
            for (var k = 0; k < soNgheSi; k++)
                db.Add(new Performance
                {
                    LoungeShowId = show.Id,
                    PerformerId = performers[(cluster * 4 + i + k * 5) % performers.Count].Id,
                    Role = k == 0 ? PerformerRole.Main : PerformerRole.Guest,
                    OrderIndex = k + 1
                });
        }

        await db.SaveChangesAsync(ct);

        // Hai hạng vé mỗi buổi, giá khác nhau theo buổi để bộ lọc giá và thống kê doanh thu có cái mà phân biệt.
        foreach (var s in staged)
        {
            var i = staged.IndexOf(s);
            var gia = new[] { 150_000m, 200_000m, 250_000m, 350_000m, 450_000m }[i % 5];
            foreach (var (ten, heSo, sucChua) in new[] { ("Phổ thông", 1m, 40), ("Bàn gần sân khấu", 1.8m, 16) })
            {
                var tier = new TicketTier { LoungeShowId = s.Show.Id, Name = ten, AccessType = AccessType.Physical, TotalCapacity = sucChua };
                db.Add(tier);
                await db.SaveChangesAsync(ct);
                var price = new TicketPrice
                {
                    TierId = tier.Id, Name = "Giá chuẩn", Price = Math.Round(gia * heSo / 10_000m) * 10_000m, Quota = sucChua,
                    IsActive = true, SaleStart = now.AddDays(-30), SaleEnd = s.Show.ScheduledStart, PurchaseChannel = PurchaseChannel.Online
                };
                db.Add(price);
                await db.SaveChangesAsync(ct);
                s.PriceIds.Add(price.Id);
            }
        }

        // DẤU NHẬN BIẾT ẨN. SaveChanges luôn ghi đè CreatedBy bằng người đang đăng nhập (ở đây là null), nên phải đặt bằng
        // lệnh cập nhật hàng loạt — đường này không đi qua bộ đóng dấu.
        var ids = staged.Select(s => s.Show.Id).ToList();
        await db.LoungeShows.Where(s => ids.Contains(s.Id)).ExecuteUpdateAsync(u => u.SetProperty(s => s.CreatedBy, markerId), ct);
        return staged;
    }

    private async Task FollowAndWishlistAsync(
        IReadOnlyList<Person> users, IReadOnlyList<MusicLoungeVenue> venues, IReadOnlyList<Staged> upcoming, Stats stats)
    {
        foreach (var p in users.Where(p => !p.Blank))
        {
            var client = _host.ClientFor(p.User.Id, "Audience");
            var i = users.ToList().IndexOf(p);

            // Bỏ lựa chọn trùng (database ít phòng trà thì hai lượt có thể rơi vào cùng một nơi): gọi trùng chỉ nhận 409.
            var theoDoi = Enumerable.Range(0, i % 3).Select(k => venues[(i + k * 2) % venues.Count].Id).Distinct();
            foreach (var loungeId in theoDoi)
            {
                var res = await client.PostAsync($"/api/v1/follows/lounges/{loungeId}", null);
                if (res.IsSuccessStatusCode) stats.Follows++;
            }

            var hop = upcoming.Where(s => s.Cluster == p.Cluster).ToList();
            var luu = hop.Count == 0 ? [] : Enumerable.Range(0, 1 + i % 3).Select(k => hop[(i + k * 3) % hop.Count].Show.Id).Distinct();
            foreach (var showId in luu)
            {
                var res = await client.PostAsync($"/api/v1/wishlist/{showId}", null);
                if (res.IsSuccessStatusCode) stats.Wishlists++;
            }
        }
    }

    private readonly Dictionary<Guid, List<Purchase>> _purchases = [];

    /// <summary>
    /// Mua vé qua đúng ba bước của người dùng thật: giữ chỗ → thanh toán → callback VNPay. Người mua nghiêng về buổi
    /// đúng cụm gu của mình (4 phần), còn 1 phần mua buổi khác cụm — không ai chỉ nghe đúng một dòng nhạc, và lọc cộng
    /// tác cần những giao điểm đó.
    /// </summary>
    private async Task SellAsync(ApplicationDbContext db, IReadOnlyList<Person> users, IReadOnlyList<Staged> shows, Stats stats, CancellationToken ct)
    {
        var buyers = users.Where(p => !p.Blank).ToList();

        for (var si = 0; si < shows.Count; si++)
        {
            var s = shows[si];
            // Buổi đã diễn bán khá (12–18 lượt mua); buổi sắp diễn rải từ 0 tới 9 để bảng thịnh hành có cao có thấp.
            var luot = s.Past ? 12 + si % 7 : (si * 5) % 10;
            var daMua = new HashSet<Guid>();

            for (var k = 0; k < luot; k++)
            {
                // Buổi sắp diễn: 4/5 lượt mua là người đúng gu. Buổi đã diễn: 3/5 — một đêm diễn thật luôn có người đi theo bạn.
                var dungGu = s.Past ? k % 5 < 3 : k % 5 != 4;
                var nhom = dungGu ? buyers.Where(b => b.Cluster == s.Cluster).ToList() : buyers;
                var buyer = nhom[(si * 3 + k * 7) % nhom.Count];
                if (!daMua.Add(buyer.User.Id)) continue;

                var purchase = await BuyAsync(buyer.User.Id, s.PriceIds[k % 4 == 3 ? 1 : 0], quantity: k % 3 == 0 ? 2 : 1);
                if (purchase is null) { stats.PurchasesRefused++; continue; }

                var confirmed = await db.Payments.AsNoTracking().AnyAsync(p => p.Id == purchase.PaymentId && p.Status == PaymentStatus.Confirmed, ct);
                if (!confirmed) { stats.PurchasesRefused++; continue; }

                stats.PurchasesOk++;
                stats.Tickets += purchase.TicketIds.Count;
                if (!_purchases.TryGetValue(s.Show.Id, out var list)) _purchases[s.Show.Id] = list = [];
                list.Add(purchase);
            }
        }
    }

    private async Task<Purchase?> BuyAsync(Guid buyerId, Guid priceId, int quantity)
    {
        var client = _host.ClientFor(buyerId, "Audience");

        var holdRes = await client.PostAsJsonAsync("/api/v1/tickets/holds", new { PriceId = priceId, Quantity = quantity });
        if (!holdRes.IsSuccessStatusCode) return null;
        var holdId = (await holdRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("holdId").GetGuid();

        var buyRes = await client.PostAsJsonAsync("/api/v1/tickets/purchase", new { HoldId = holdId });
        if (!buyRes.IsSuccessStatusCode) return null;
        var data = (await buyRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var orderId = data.GetProperty("orderId").GetString();
        var amount = data.GetProperty("amount").GetDecimal();

        await client.GetAsync(
            $"/api/v1/payments/vnpay/callback?vnp_TxnRef={orderId}&vnp_ResponseCode=00&vnp_Amount={(long)(amount * 100)}");

        return new Purchase(
            data.GetProperty("paymentId").GetGuid(),
            data.GetProperty("ticketIds").EnumerateArray().Select(t => t.GetGuid()).ToList(),
            buyerId);
    }

    /// <summary>
    /// Nhật ký hành vi chỉ ghi cho người ĐÃ đồng ý phân tích hành vi — đúng như job ghi nhật ký của hệ thống làm. Sinh cho
    /// cả người chưa đồng ý là dựng ra một tình trạng không thể xảy ra thật. Rải trong 14 ngày: bảng thịnh hành tính theo
    /// suy giảm mũ (bán rã 72 giờ), dồn hết vào một mốc thì mọi buổi cùng điểm.
    /// </summary>
    private async Task WriteBehaviourLogsAsync(ApplicationDbContext db, IReadOnlyList<Person> users, IReadOnlyList<Staged> shows, DateTimeOffset now, CancellationToken ct)
    {
        var upcoming = shows.Where(s => !s.Past).ToList();
        var actions = new[] { BehaviourAction.ViewEvent, BehaviourAction.ViewEventLong, BehaviourAction.ViewLineup, BehaviourAction.ClickTicket, BehaviourAction.ViewVenue, BehaviourAction.ShareEvent };

        foreach (var p in users.Where(p => p.AiConsent))
        {
            var i = users.ToList().IndexOf(p);
            var hop = upcoming.Where(s => s.Cluster == p.Cluster).ToList();
            var soDong = 6 + i % 7; // đều vượt ngưỡng RecommendationRefresh.MinBehaviourLogs (5)
            for (var k = 0; k < soDong; k++)
            {
                var pool = k % 4 == 3 || hop.Count == 0 ? upcoming : hop; // phần lớn đúng gu, đôi khi ghé buổi khác
                db.Add(new UserBehaviourLog
                {
                    UserId = p.User.Id,
                    LoungeShowId = pool[(i * 5 + k * 3) % pool.Count].Show.Id,
                    Action = actions[(i + k) % actions.Length],
                    DurationSeconds = actions[(i + k) % actions.Length] == BehaviourAction.ViewEventLong ? 45 + (i * k) % 120 : null,
                    CreatedAt = now.AddDays(-((i + k * 2) % 14)).AddHours(-(k * 5 % 23))
                });
            }

            foreach (var (showId, list) in _purchases)
                if (list.Any(x => x.BuyerId == p.User.Id))
                    db.Add(new UserBehaviourLog { UserId = p.User.Id, LoungeShowId = showId, Action = BehaviourAction.PurchaseTicket, CreatedAt = now.AddDays(-(i % 10)) });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Biến các buổi "đã diễn" thành đã diễn THẬT theo luật của hệ thống:
    /// soát vé (khoảng 85% người mua tới) → lùi giờ diễn về vừa qua → <see cref="LoungeShowLifecycle.TryMarkEnded"/> kết
    /// thúc buổi và mở cửa sổ đánh giá → người đã vào cửa gửi đánh giá qua API → lùi cả cụm về đúng ngày trong quá khứ.
    /// </summary>
    private async Task FinishPastShowsAsync(
        IServiceProvider sp, ApplicationDbContext db, IReadOnlyList<Person> users, IReadOnlyList<Staged> past, Stats stats,
        DateTimeOffset now, CancellationToken ct)
    {
        if (past.Count == 0) return;
        var pastIds = past.Select(s => s.Show.Id).ToList();
        var nameById = users.ToDictionary(u => u.User.Id);

        // 1. Soát vé. Người không tới giữ vé Confirmed (vắng mặt) — và theo luật thì không được đánh giá.
        var daVao = new Dictionary<Guid, HashSet<Guid>>(); // showId → người đã vào cửa
        foreach (var s in past)
        {
            daVao[s.Show.Id] = [];
            var list = _purchases.GetValueOrDefault(s.Show.Id) ?? [];
            for (var k = 0; k < list.Count; k++)
            {
                if (k % 7 == 6) continue; // khoảng 1/7 lượt mua không tới
                var ticketIds = list[k].TicketIds;
                await db.Tickets.Where(t => ticketIds.Contains(t.Id)).ExecuteUpdateAsync(u => u.SetProperty(t => t.Status, TicketStatus.Used), ct);
                await db.Set<PhysicalTicketDetail>().Where(d => ticketIds.Contains(d.TicketId))
                    .ExecuteUpdateAsync(u => u.SetProperty(d => d.CheckedInAt, now.AddHours(-9)), ct);
                daVao[s.Show.Id].Add(list[k].BuyerId);
            }
        }

        // 2. Lùi giờ diễn: kết thúc từ 7 giờ trước — vừa qua hạn tự kết thúc (ShowAutoEndGrace, mặc định 6 giờ).
        await db.LoungeShows.Where(s => pastIds.Contains(s.Id)).ExecuteUpdateAsync(u => u
            .SetProperty(s => s.ScheduledStart, now.AddHours(-9.5))
            .SetProperty(s => s.ScheduledEnd, now.AddHours(-7))
            .SetProperty(s => s.ActualStart, now.AddHours(-9.5)), ct);

        // 3. Kết thúc buổi diễn bằng ĐÚNG hàm chuyển trạng thái của hệ thống, với đúng hai tham số AutoEndStaleShowsJob
        // truyền: mốc kết thúc = giờ kết thúc theo lịch, cửa sổ đánh giá đọc từ system_config.
        // Cố ý KHÔNG chạy cả AutoEndStaleShowsJob: job đó kết thúc MỌI buổi quá hạn trong database, kể cả buổi không phải
        // của bộ mẫu. Bản đầu của bộ dựng làm vậy và trên database dùng chung của bộ test nó đổi trạng thái buổi diễn của
        // các test khác (hỏng hàng loạt). Bộ dựng chỉ được chạm vào dòng của chính nó.
        // Xoá bộ theo dõi trước khi nạp lại: các buổi đã nạp lúc tạo vẫn mang ngày CŨ trong bộ nhớ.
        db.ChangeTracker.Clear();
        var ratingWindowDays = await sp.GetRequiredService<ISystemConfigService>().GetIntAsync(ConfigKeys.RatingWindowDays, 7, ct);
        foreach (var show in await db.LoungeShows.Where(s => pastIds.Contains(s.Id)).ToListAsync(ct))
            LoungeShowLifecycle.TryMarkEnded(show, ShowSchedule.EffectiveEnd(show), ratingWindowDays);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        var chuaKetThuc = await db.LoungeShows.CountAsync(s => pastIds.Contains(s.Id) && s.Status != LoungeShowStatus.Ended, ct);
        if (chuaKetThuc > 0) _log($"CẢNH BÁO: {chuaKetThuc} buổi mẫu chưa kết thúc được — các buổi đó sẽ không có đánh giá.");

        // 4. Đánh giá qua API. Mỗi buổi có một "chất lượng" riêng để điểm trung bình các buổi khác nhau thật.
        foreach (var s in past)
        {
            var k = 0;
            foreach (var buyerId in daVao[s.Show.Id])
            {
                k++;
                if (k % 7 == 0) continue; // không phải ai đi xem cũng đánh giá
                var sao = Math.Clamp(s.Quality + new[] { 1, 0, 1, -1, 0, 1, 2, -2 }[(k + s.DaysAgo) % 8], 1, 5);
                var loi = Comments[sao];
                // Khoảng 1/4 chỉ chấm sao, không viết gì.
                var comment = k % 4 == 3 ? null : loi[(k + s.DaysAgo) % loi.Length];

                var res = await _host.ClientFor(buyerId, "Audience")
                    .PostAsJsonAsync($"/api/v1/lounge-shows/{s.Show.Id}/rate", new { Score = sao, Comment = comment });
                if (res.IsSuccessStatusCode) stats.Ratings++; else stats.RatingsRefused++;
            }
        }

        // 5. Lùi cả cụm của từng buổi về đúng ngày trong quá khứ. Dời NGUYÊN KHỐI theo một khoảng: không con số tiền nào đổi.
        // Nạp dòng lên rồi lưu lại (không dùng phép tính ngày trong câu lệnh cập nhật hàng loạt): phép trừ DateTimeOffset
        // không dịch được trên SQLite của bộ test, mà đoạn này phải chạy được ở cả hai nơi.
        db.ChangeTracker.Clear();
        foreach (var s in past)
        {
            var lui = TimeSpan.FromDays(s.DaysAgo);
            var show = await db.LoungeShows.SingleAsync(x => x.Id == s.Show.Id, ct);
            show.ScheduledStart -= lui;
            show.ScheduledEnd -= lui;
            show.ActualStart -= lui;
            show.ActualEnd -= lui;
            show.RatingOpenUntil -= lui;

            // Đánh giá được viết trong vài ngày sau đêm diễn.
            var ratings = await db.Ratings.Where(r => r.LoungeShowId == show.Id).ToListAsync(ct);
            for (var k = 0; k < ratings.Count; k++)
                ratings[k].CreatedAt = show.ActualEnd!.Value.UtcDateTime.AddHours(2 + (k * 11) % 96);

            var list = _purchases.GetValueOrDefault(show.Id) ?? [];
            for (var k = 0; k < list.Count; k++)
            {
                // Vé mua trước đêm diễn từ 1 tới 12 ngày; soát vé đúng tối hôm diễn.
                await ShiftPurchaseAsync(db, list[k], lui + TimeSpan.FromDays(1 + (k * 5) % 12), ct);
                var ticketIds = list[k].TicketIds;
                var chiTiet = await db.PhysicalTicketDetails.Where(d => ticketIds.Contains(d.TicketId) && d.CheckedInAt != null).ToListAsync(ct);
                foreach (var d in chiTiet) d.CheckedInAt = show.ScheduledStart.AddMinutes(-20 + (k * 7) % 50);
            }

            await db.SaveChangesAsync(ct);
        }

        // Dấu nhận biết: SaveChanges vừa rồi không đụng CreatedBy (chỉ đóng dấu khi THÊM), nhưng kiểm lại cho chắc vì
        // đường dọn sống nhờ nó.
        var pastIdsCheck = past.Select(s => s.Show.Id).ToList();
        if (await db.LoungeShows.AnyAsync(x => pastIdsCheck.Contains(x.Id) && x.CreatedBy == null, ct))
            throw new InvalidOperationException("Buổi mẫu mất dấu nhận biết CreatedBy sau khi lùi ngày.");
    }

    /// <summary>Vé của buổi SẮP diễn: rải thời điểm mua trong 13 ngày gần đây, để thịnh hành và tốc độ bán vé có độ dốc.</summary>
    private async Task SpreadUpcomingPurchasesAsync(ApplicationDbContext db, IReadOnlyList<Staged> upcoming, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var s in upcoming)
        {
            var list = _purchases.GetValueOrDefault(s.Show.Id) ?? [];
            for (var k = 0; k < list.Count; k++)
                await ShiftPurchaseAsync(db, list[k], TimeSpan.FromHours(3 + (k * 37 + s.Cluster * 11) % (13 * 24)), ct);
        }
    }

    /// <summary>
    /// Dời MỘT lần mua về quá khứ: thanh toán, vé, bút toán sổ cái và lịch quyết toán của nó lùi cùng một khoảng. Chỉ đổi
    /// mốc thời gian — số tiền, tài khoản, trạng thái giữ nguyên như hệ thống đã ghi.
    /// </summary>
    private static async Task ShiftPurchaseAsync(ApplicationDbContext db, Purchase p, TimeSpan lui, CancellationToken ct)
    {
        var payment = await db.Payments.SingleAsync(x => x.Id == p.PaymentId, ct);
        payment.CreatedAt -= lui;
        payment.PaidAt -= lui;

        foreach (var t in await db.Tickets.Where(x => p.TicketIds.Contains(x.Id)).ToListAsync(ct)) t.CreatedAt -= lui;
        foreach (var e in await db.LedgerEntries.Where(x => x.PaymentId == p.PaymentId).ToListAsync(ct)) e.CreatedAt -= lui;
        foreach (var x in await db.Settlements.Where(x => x.PaymentId == p.PaymentId).ToListAsync(ct))
        {
            x.CreatedAt -= lui;
            x.ScheduledAt -= lui;
        }

        await db.SaveChangesAsync(ct);
    }

    // ---------- dọn ----------

    /// <summary>
    /// Xoá theo đúng chiều khoá ngoại, từ dòng phụ thuộc ngược lên gốc. Mọi thứ tìm lại được qua ba dấu nhận biết (tài
    /// khoản đánh dấu, đuôi email, CreatedBy của buổi diễn) nên không phải đoán và không chạm vào dữ liệu thật.
    ///
    /// CHÚ Ý: nếu sau khi dựng có NGƯỜI THẬT mua vé / đánh giá một buổi mẫu thì vé, thanh toán, sổ cái và đánh giá đó cũng
    /// bị xoá theo buổi diễn — buổi diễn không còn thì chúng không còn nghĩa. Số dòng như vậy được in ra trước khi xoá.
    /// </summary>
    public async Task<(int Shows, int Users)> CleanAsync(CancellationToken ct = default)
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var marker = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == MarkerEmail, ct);
        var userIds = await db.Users.Where(u => u.Email.EndsWith(EmailDomain)).Select(u => u.Id).ToListAsync(ct);
        var showIds = marker is null ? [] : await db.LoungeShows.Where(s => s.CreatedBy == marker.Id).Select(s => s.Id).ToListAsync(ct);

        if (userIds.Count == 0 && showIds.Count == 0)
        {
            _log("Không tìm thấy dữ liệu mẫu nào để xoá.");
            return (0, 0);
        }

        // Lấy khoá con trước rồi xoá theo khoá: lệnh xoá hàng loạt không dịch được phép nối bảng trên mọi provider.
        var tierIds = await db.Set<TicketTier>().Where(t => showIds.Contains(t.LoungeShowId)).Select(t => t.Id).ToListAsync(ct);
        var priceIds = await db.Set<TicketPrice>().Where(p => tierIds.Contains(p.TierId)).Select(p => p.Id).ToListAsync(ct);
        var tickets = await db.Tickets
            .Where(t => showIds.Contains(t.ShowId) || (t.BuyerId != null && userIds.Contains(t.BuyerId.Value)))
            .Select(t => new { t.Id, t.PaymentId, t.BuyerId }).ToListAsync(ct);
        var ticketIds = tickets.Select(t => t.Id).ToList();
        var paymentIds = tickets.Where(t => t.PaymentId != null).Select(t => t.PaymentId!.Value).Distinct().ToList();
        var ratingIds = await db.Ratings
            .Where(r => showIds.Contains(r.LoungeShowId) || (r.UserId != null && userIds.Contains(r.UserId.Value)))
            .Select(r => r.Id).ToListAsync(ct);

        var cuaNguoiThat = tickets.Count(t => t.BuyerId == null || !userIds.Contains(t.BuyerId.Value));
        if (cuaNguoiThat > 0)
            _log($"Trong các buổi mẫu có {cuaNguoiThat} vé KHÔNG thuộc tài khoản mẫu — xoá theo buổi diễn.");

        await db.ContentReports.Where(c => (c.TargetType == ReportTargetType.Rating && ratingIds.Contains(c.TargetId))
            || (c.TargetType == ReportTargetType.Show && showIds.Contains(c.TargetId))).ExecuteDeleteAsync(ct);
        await db.Ratings.Where(r => ratingIds.Contains(r.Id)).ExecuteDeleteAsync(ct);
        await db.AiRecommendations.Where(r => userIds.Contains(r.UserId) || showIds.Contains(r.LoungeShowId)).ExecuteDeleteAsync(ct);
        await db.UserEventScores.Where(x => userIds.Contains(x.UserId) || showIds.Contains(x.ShowId)).ExecuteDeleteAsync(ct);
        await db.BehaviourLogs.Where(b => userIds.Contains(b.UserId) || showIds.Contains(b.LoungeShowId)).ExecuteDeleteAsync(ct);
        await db.Wishlists.Where(w => userIds.Contains(w.UserId) || showIds.Contains(w.LoungeShowId)).ExecuteDeleteAsync(ct);
        await db.Follows.Where(f => userIds.Contains(f.UserId)).ExecuteDeleteAsync(ct);
        await db.Notifications.Where(n => userIds.Contains(n.UserId)).ExecuteDeleteAsync(ct);

        // Tiền: quyết toán và sổ cái trỏ về thanh toán; vé trỏ về thanh toán. Xoá con trước.
        await db.Settlements.Where(x => paymentIds.Contains(x.PaymentId)).ExecuteDeleteAsync(ct);
        await db.LedgerEntries.Where(e => e.PaymentId != null && paymentIds.Contains(e.PaymentId.Value)).ExecuteDeleteAsync(ct);
        await db.PhysicalTicketDetails.Where(d => ticketIds.Contains(d.TicketId)).ExecuteDeleteAsync(ct);
        await db.Tickets.Where(t => ticketIds.Contains(t.Id)).ExecuteDeleteAsync(ct);
        await db.TicketHolds.Where(h => priceIds.Contains(h.PriceId) || userIds.Contains(h.UserId)).ExecuteDeleteAsync(ct);
        await db.Payments.Where(p => paymentIds.Contains(p.Id)).ExecuteDeleteAsync(ct);

        await db.Set<UserFavouriteGenre>().Where(g => userIds.Contains(g.UserId)).ExecuteDeleteAsync(ct);
        await db.Set<UserFavouriteMood>().Where(m => userIds.Contains(m.UserId)).ExecuteDeleteAsync(ct);
        await db.Set<UserFavouriteAtmosphere>().Where(a => userIds.Contains(a.UserId)).ExecuteDeleteAsync(ct);

        await db.EventModerations.Where(m => showIds.Contains(m.TargetId)).ExecuteDeleteAsync(ct);
        await db.Performances.Where(x => showIds.Contains(x.LoungeShowId)).ExecuteDeleteAsync(ct);
        await db.Set<LoungeShowGenre>().Where(g => showIds.Contains(g.LoungeShowId)).ExecuteDeleteAsync(ct);
        await db.Set<LoungeShowMood>().Where(m => showIds.Contains(m.LoungeShowId)).ExecuteDeleteAsync(ct);
        await db.Set<LoungeShowAtmosphere>().Where(a => showIds.Contains(a.LoungeShowId)).ExecuteDeleteAsync(ct);
        await db.Set<TicketPrice>().Where(p => priceIds.Contains(p.Id)).ExecuteDeleteAsync(ct);
        await db.Set<TicketTier>().Where(t => tierIds.Contains(t.Id)).ExecuteDeleteAsync(ct);
        await db.LoungeShows.Where(s => showIds.Contains(s.Id)).ExecuteDeleteAsync(ct);

        if (marker is not null)
            await db.Performers.Where(x => x.CreatedByUserId == marker.Id).ExecuteDeleteAsync(ct);
        await db.Users.Where(u => userIds.Contains(u.Id)).ExecuteDeleteAsync(ct);

        _log($"Đã xoá {showIds.Count} buổi diễn, {userIds.Count} tài khoản, {ticketIds.Count} vé, {paymentIds.Count} thanh toán, {ratingIds.Count} đánh giá mẫu.");
        return (showIds.Count, userIds.Count);
    }

    /// <summary>Số bút toán (theo JournalId) của vé mẫu mà tổng nợ khác tổng có. Phải bằng 0.</summary>
    private static async Task<int> UnbalancedJournalsAsync(ApplicationDbContext db, List<Guid> showIds, CancellationToken ct)
    {
        var paymentIds = await db.Tickets.Where(t => showIds.Contains(t.ShowId) && t.PaymentId != null).Select(t => t.PaymentId!.Value).Distinct().ToListAsync(ct);
        var entries = await db.Set<LedgerEntry>().Where(e => e.PaymentId != null && paymentIds.Contains(e.PaymentId.Value))
            .Select(e => new { e.JournalId, e.Amount, e.IsDebit }).ToListAsync(ct);
        return entries.GroupBy(e => e.JournalId).Count(g => g.Sum(e => e.IsDebit ? e.Amount : -e.Amount) != 0m);
    }
}

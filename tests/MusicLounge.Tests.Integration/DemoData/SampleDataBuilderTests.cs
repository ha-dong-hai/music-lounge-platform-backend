using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace MusicLounge.Tests.Integration.DemoData;

/// <summary>
/// MLACP-577. Chạy ĐÚNG đoạn mã dựng dữ liệu mẫu trên database của bộ test (SQLite), qua ứng dụng thật của
/// <see cref="ApiFactory"/>. Bài kiểm tra này là thứ chứng minh ba lời hứa của bộ dựng: vé đi kèm thanh toán và sổ cái
/// CÂN, buổi đã diễn do chính job của hệ thống kết thúc và có đánh giá, và đường dọn xoá sạch đúng những gì đã dựng.
///
/// Chạy trên máy chủ DÙNG CHUNG của collection "Integration" (tuần tự với mọi test khác) và LUÔN dọn trong finally: bộ
/// dựng thêm vài chục buổi diễn và vài trăm vé — để sót lại thì các test đếm "tổng số buổi" khác sẽ lệch. Bản đầu dựng
/// một máy chủ riêng chạy song song và làm hỏng hàng loạt test khác (hai máy chủ ứng dụng cùng tiến trình giẫm lên trạng
/// thái tĩnh của nhau).
///
/// GIỚI HẠN: SQLite không kiểm khoá ngoại/kiểu dữ liệu như SQL Server. Thứ tự xoá của đường dọn và các lệnh dời ngày
/// phải được diễn tập thêm trên SQL Server thật trước khi chạy lên môi trường thật (xem scripts/demo-data.ps1).
/// </summary>
[Collection("Integration")]
public sealed class SampleDataBuilderTests
{
    private readonly ApiFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly List<string> _log = [];

    public SampleDataBuilderTests(ApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private SampleDataBuilder Builder() => new(new ApiFactorySampleHost(_factory), s => { _log.Add(s); _output.WriteLine(s); });

    private ApplicationDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    [Fact]
    public async Task Seed_DungDuLieuDaDang_TienDoHeThongTinh_RoiDonSach()
    {
        int showsTruoc, usersTruoc, paymentsTruoc, ledgerTruoc, ticketsTruoc;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            showsTruoc = await db.LoungeShows.CountAsync();
            usersTruoc = await db.Users.CountAsync();
            paymentsTruoc = await db.Payments.CountAsync();
            ledgerTruoc = await db.LedgerEntries.CountAsync();
            ticketsTruoc = await db.Tickets.CountAsync();
        }

        try
        {
        (await Builder().SeedAsync()).Should().BeTrue(string.Join(" | ", _log));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            var marker = await db.Users.SingleAsync(u => u.Email == SampleDataBuilder.MarkerEmail);
            marker.IsActive.Should().BeFalse("tài khoản đánh dấu không được đăng nhập được");

            var shows = await db.LoungeShows.Where(s => s.CreatedBy == marker.Id).ToListAsync();
            shows.Should().HaveCount(55);
            shows.Should().OnlyContain(s => !s.Name.Contains("[DEMO]") && !s.Name.Contains("test", StringComparison.OrdinalIgnoreCase),
                "dữ liệu mẫu phải mang tên như thật");
            shows.Count(s => s.Status == LoungeShowStatus.Published).Should().Be(40, "40 buổi sắp diễn");

            // Buổi đã diễn: do CHÍNH job của hệ thống kết thúc, lùi về quá khứ, cửa sổ đánh giá đặt theo giờ kết thúc.
            var daDien = shows.Where(s => s.Status == LoungeShowStatus.Ended).ToList();
            daDien.Should().HaveCount(15);
            daDien.Should().OnlyContain(s => s.ActualEnd != null && s.RatingOpenUntil != null && s.ScheduledStart < DateTimeOffset.UtcNow.AddDays(-2));
            daDien.Select(s => s.ScheduledStart.Date).Distinct().Count().Should().BeGreaterThan(10, "các đêm đã diễn rải trên nhiều ngày");

            // Đa dạng thể loại: không còn chuyện cả sàn chỉ có một dòng nhạc.
            var showIds = shows.Select(s => s.Id).ToList();
            var soTheLoai = await db.LoungeShowGenres.Where(g => showIds.Contains(g.LoungeShowId)).Select(g => g.GenreId).Distinct().CountAsync();
            soTheLoai.Should().BeGreaterThan(1);

            // TIỀN: mỗi vé mẫu có thanh toán Confirmed; mỗi thanh toán có bút toán; mọi bút toán CÂN; có lịch quyết toán.
            var tickets = await db.Tickets.Where(t => showIds.Contains(t.ShowId)).ToListAsync();
            tickets.Count.Should().BeGreaterThan(100);
            tickets.Should().OnlyContain(t => t.PaymentId != null);
            var paymentIds = tickets.Select(t => t.PaymentId!.Value).Distinct().ToList();
            var payments = await db.Payments.Where(p => paymentIds.Contains(p.Id)).ToListAsync();
            payments.Should().OnlyContain(p => p.Status == PaymentStatus.Confirmed && p.GrossAmount > 0 && p.NetAmount > 0 && p.NetAmount < p.GrossAmount);
            payments.Should().OnlyContain(p => p.GrossAmount == Math.Truncate(p.GrossAmount) && p.NetAmount == Math.Truncate(p.NetAmount),
                "tiền đồng không có số lẻ");
            payments.Select(p => p.PaidAt!.Value.Date).Distinct().Count().Should().BeGreaterThan(8, "thời điểm mua phải rải ra, không dồn một ngày");

            var entries = await db.LedgerEntries.Where(e => e.PaymentId != null && paymentIds.Contains(e.PaymentId.Value)).ToListAsync();
            entries.Select(e => e.PaymentId).Distinct().Count().Should().Be(payments.Count, "thanh toán nào cũng có bút toán sổ cái");
            entries.GroupBy(e => e.JournalId).Should().OnlyContain(g => g.Sum(e => e.IsDebit ? e.Amount : -e.Amount) == 0m, "sổ kép: nợ = có");
            (await db.Settlements.CountAsync(x => paymentIds.Contains(x.PaymentId))).Should().BeGreaterThan(0);

            // Vé của buổi đã diễn phần lớn đã soát; buổi sắp diễn thì chưa.
            var daDienIds = daDien.Select(s => s.Id).ToHashSet();
            tickets.Where(t => daDienIds.Contains(t.ShowId)).Count(t => t.Status == TicketStatus.Used).Should().BeGreaterThan(50);
            tickets.Where(t => !daDienIds.Contains(t.ShowId)).Should().OnlyContain(t => t.Status == TicketStatus.Confirmed);

            // ĐÁNH GIÁ: chỉ ở buổi đã diễn, đủ các mức sao, có lời và không lời.
            var ratings = await db.Ratings.Where(r => showIds.Contains(r.LoungeShowId)).ToListAsync();
            ratings.Count.Should().BeGreaterThan(50);
            ratings.Should().OnlyContain(r => daDienIds.Contains(r.LoungeShowId));
            ratings.Select(r => r.Score).Distinct().Count().Should().BeGreaterThanOrEqualTo(4);
            ratings.Should().Contain(r => r.Comment == null).And.Contain(r => r.Comment != null && r.Comment.Length > 200);

            // Khán giả: đủ các nhóm mà hệ gợi ý phân biệt.
            var users = await db.Users.Where(u => u.Email.EndsWith(SampleDataBuilder.EmailDomain) && u.Email != SampleDataBuilder.MarkerEmail).ToListAsync();
            users.Should().HaveCount(40);
            users.Should().Contain(u => u.AiConsent).And.Contain(u => !u.AiConsent);
            var userIds = users.Select(u => u.Id).ToList();
            (await db.Follows.CountAsync(f => userIds.Contains(f.UserId))).Should().BeGreaterThan(10);
            (await db.Wishlists.CountAsync(w => userIds.Contains(w.UserId))).Should().BeGreaterThan(30);
            var logsByUser = await db.BehaviourLogs.Where(b => userIds.Contains(b.UserId)).GroupBy(b => b.UserId).Select(g => g.Count()).ToListAsync();
            logsByUser.Should().OnlyContain(n => n >= 5, "ngưỡng RecommendationRefresh.MinBehaviourLogs");
            var khongDongY = users.Where(u => !u.AiConsent).Select(u => u.Id).ToList();
            (await db.BehaviourLogs.AnyAsync(b => khongDongY.Contains(b.UserId))).Should().BeFalse("không ghi hành vi của người chưa đồng ý");
            (await db.UserEventScores.CountAsync(x => userIds.Contains(x.UserId))).Should().BeGreaterThan(10, "ngưỡng huấn luyện lọc cộng tác");
        }

        // Chạy lần hai phải từ chối, không dựng chồng.
        (await Builder().SeedAsync()).Should().BeFalse();
        }
        catch
        {
            // Hỏng giữa chừng vẫn phải trả database dùng chung về nguyên trạng cho các test sau.
            await Builder().CleanAsync();
            throw;
        }

        // DỌN: về đúng số dòng ban đầu ở các bảng gốc và bảng tiền.
        var (soBuoi, soNguoi) = await Builder().CleanAsync();
        soBuoi.Should().Be(55);
        soNguoi.Should().Be(41);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            (await db.LoungeShows.CountAsync()).Should().Be(showsTruoc);
            (await db.Users.CountAsync()).Should().Be(usersTruoc);
            (await db.Payments.CountAsync()).Should().Be(paymentsTruoc);
            (await db.LedgerEntries.CountAsync()).Should().Be(ledgerTruoc);
            (await db.Tickets.CountAsync()).Should().Be(ticketsTruoc);
            (await db.Performers.AnyAsync(p => p.Name == "Thu Hà")).Should().BeFalse();
        }
    }
}

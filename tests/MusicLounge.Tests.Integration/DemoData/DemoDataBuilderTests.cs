using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using Xunit.Abstractions;

namespace MusicLounge.Tests.Integration.DemoData;

/// <summary>
/// MLACP-324. Kiểm chứng bộ sinh dữ liệu trình diễn trên một database thật của bộ test.
///
/// <b>Vì sao bài kiểm tra này quan trọng hơn vẻ ngoài của nó.</b> Thứ nguy hiểm nhất ở một script
/// sinh dữ liệu không phải là nó sinh sai — sinh sai thì thấy ngay. Nguy hiểm là <b>nó dọn không
/// sạch</b>: vài dòng sót lại, không ai biết, và từ đó về sau không ai dám xoá vì không rõ dòng nào
/// là thật dòng nào là giả. Nên phần được kiểm kỹ nhất ở đây là đường dọn, không phải đường sinh.
///
/// Bài kiểm tra chạy đúng lớp mà script chạy, trên database thật (SQLite của bộ test) chứ không
/// phải bản giả lập — nên nếu thiếu một cột bắt buộc, sai chiều khoá ngoại, hay bỏ sót một bảng lúc
/// dọn, thì ở đây đỏ, chứ không phải đỏ lúc đã chạy vào database của đồ án.
/// </summary>
[Collection("Integration")]
public sealed class DemoDataBuilderTests
{
    private readonly ApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public DemoDataBuilderTests(ApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private sealed record Counts(
        int Users, int Shows, int Tickets, int Wishlists, int BehaviourLogs,
        int Tiers, int Prices, int ShowGenres, int FavouriteGenres);

    private static async Task<Counts> CountAsync(ApplicationDbContext db) => new(
        await db.Users.CountAsync(),
        await db.LoungeShows.CountAsync(),
        await db.Tickets.CountAsync(),
        await db.Wishlists.CountAsync(),
        await db.BehaviourLogs.CountAsync(),
        await db.Set<TicketTier>().CountAsync(),
        await db.Set<TicketPrice>().CountAsync(),
        await db.Set<LoungeShowGenre>().CountAsync(),
        await db.Set<UserFavouriteGenre>().CountAsync());

    [Fact]
    public async Task SeedsTheInputsEveryAiBranchNeeds_AndCleansUpAfterItselfCompletely()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var builder = new DemoDataBuilder(db, _output.WriteLine);

        var before = await CountAsync(db);

        // ---------- sinh ----------

        (await builder.SeedAsync()).Should().BeTrue("bộ test có sẵn phòng trà đã duyệt và taxonomy");

        var demoUsers = await db.Users
            .Where(u => u.Email.EndsWith(DemoDataBuilder.EmailDomain))
            .ToListAsync();
        var demoShowIds = await db.LoungeShows
            .Where(s => s.Name.StartsWith(DemoDataBuilder.ShowPrefix))
            .Select(s => s.Id)
            .ToListAsync();

        demoUsers.Should().HaveCount(12);
        demoShowIds.Should().HaveCount(12);

        // Bốn nhóm phải cùng tồn tại, nếu không thì có nhánh của hệ gợi ý không có gì để trình diễn.
        demoUsers.Count(u => u.AiConsent).Should().Be(10, "nhóm đã đồng ý cho phân tích hành vi");
        demoUsers.Count(u => !u.AiConsent).Should().Be(2, "nhóm chứng minh ranh giới đồng ý");

        var declared = await db.Set<UserFavouriteGenre>()
            .Where(g => demoUsers.Select(u => u.Id).Contains(g.UserId))
            .Select(g => g.UserId)
            .Distinct()
            .CountAsync();
        declared.Should().Be(9, "9 người khai gu, 3 người cố ý để trống cho tình huống cold start");

        var withoutDeclaredTaste = demoUsers.Select(u => u.Id).Except(
            await db.Set<UserFavouriteGenre>()
                .Where(g => demoUsers.Select(u => u.Id).Contains(g.UserId))
                .Select(g => g.UserId).ToListAsync()).ToList();

        // Cold start suy từ lịch sử chỉ chạy được nếu người chưa khai gu VẪN có giao dịch. Không có
        // điều đó thì nhóm này chỉ chứng minh được đường trending, không chứng minh được MLACP-321.
        (await db.Wishlists.CountAsync(w => withoutDeclaredTaste.Contains(w.UserId))
         + await db.Tickets.CountAsync(t => t.BuyerId != null && withoutDeclaredTaste.Contains(t.BuyerId.Value)))
            .Should().BeGreaterThan(0, "người chưa khai gu vẫn phải có lịch sử để suy ra");

        // Nhật ký hành vi chỉ được ghi cho người đã đồng ý — nếu sai thì bộ dữ liệu đi trình diễn
        // ranh giới đồng ý lại tự vi phạm chính ranh giới đó.
        var notConsenting = demoUsers.Where(u => !u.AiConsent).Select(u => u.Id).ToList();
        (await db.BehaviourLogs.CountAsync(b => notConsenting.Contains(b.UserId)))
            .Should().Be(0, "không ghi nhật ký hành vi của người chưa đồng ý");

        // Điểm số phải do job thật tính ra, không phải do script viết thẳng vào.
        var demoUserIds = demoUsers.Select(u => u.Id).ToList();
        (await db.Set<UserEventScore>().CountAsync(s => demoUserIds.Contains(s.UserId)))
            .Should().BeGreaterThan(0, "job tính điểm thật phải chạy được trên dữ liệu vừa sinh");

        // ---------- dọn ----------

        var (removedShows, removedUsers) = await builder.CleanAsync();
        removedShows.Should().Be(12);
        removedUsers.Should().Be(12);

        // Đây là phần đáng giá nhất: mọi bảng phải trở về đúng con số trước khi sinh. Sót một dòng
        // ở bất kỳ bảng nào cũng làm bài này đỏ.
        (await CountAsync(db)).Should().BeEquivalentTo(before,
            "dọn xong phải không còn dấu vết nào, nếu không thì sau này không ai dám xoá gì nữa");

        (await db.Set<UserEventScore>().CountAsync(s => demoUserIds.Contains(s.UserId)))
            .Should().Be(0, "điểm số do job sinh ra cũng là dữ liệu demo, phải đi cùng");
    }

    [Fact]
    public async Task RunningCleanWhenThereIsNothingToCleanIsHarmless()
    {
        // Đường dọn sẽ bị chạy lại nhiều lần, kể cả khi không có gì để xoá. Nó phải không làm gì
        // thay vì lỗi hoặc xoá nhầm.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var before = await CountAsync(db);
        var (shows, users) = await new DemoDataBuilder(db, _output.WriteLine).CleanAsync();

        shows.Should().Be(0);
        users.Should().Be(0);
        (await CountAsync(db)).Should().BeEquivalentTo(before);
    }
}

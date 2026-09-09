using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Lounges;

/// <summary>
/// BR-01, MLACP-307.
///
/// <c>MusicLounge.Status</c> mặc định là <c>Pending</c>, doc comment của POST /lounges ghi rõ "phòng
/// trà mới luôn ở trạng thái chờ Admin duyệt", và tài liệu BA mô tả luồng duyệt này như thể nó đang
/// chạy. Không có gì trong số đó là thật: không tồn tại endpoint nào duyệt được một phòng trà, nên
/// hồ sơ nộp lên rồi nằm ở Pending vĩnh viễn — và vì Pending không chặn thứ gì, phòng trà chưa ai
/// xác minh vẫn nằm trong danh sách công khai và vẫn mở bán vé thu tiền thật.
///
/// Đây là dạng lỗi khó thấy nhất trong cả codebase này: mọi thứ chạy đúng như mong đợi, chỉ là quy
/// tắc mà ai cũng tưởng đang có hiệu lực thì chưa từng được viết ra. Chính bộ test này cũng đang
/// chạy trên hai venue ở trạng thái Pending suốt từ đầu mà không ai nhận ra.
/// </summary>
[Collection("Integration")]
public sealed class VenueApprovalGateTests
{
    private readonly ApiFactory _factory;

    public VenueApprovalGateTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
    private HttpClient Anonymous() => _factory.CreateClient();

    private async Task<(int LoungeId, int OwnerId, string Name)> SeedVenueAsync(
        LoungeStatus status, string? businessLicenseUrl = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User
        {
            Email = $"venue-owner-{Guid.NewGuid():N}@test.com",
            FullName = "Chủ Phòng Trà",
            Role = UserRole.Owner,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var name = $"Venue-{Guid.NewGuid():N}";
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id,
            Name = name,
            Description = "Integration test venue",
            Status = status,
            BusinessLicenseUrl = businessLicenseUrl,
            Address = new VenueAddress { Street = "1 Thử Nghiệm", Ward = "P1", District = "Q1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        return (lounge.Id, owner.Id, name);
    }

    private async Task<int> SeedDraftShowAsync(int loungeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = loungeId,
            Name = $"GateShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Draft,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(30),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(30).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task<LoungeStatus> StatusOfAsync(int loungeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Set<MusicLoungeVenue>().AsNoTracking().SingleAsync(l => l.Id == loungeId)).Status;
    }

    // ---------- điều nghiêm trọng nhất: bán vé ----------

    [Fact]
    public async Task AVenueNobodyHasApprovedYet_CannotOpenAShowForSale()
    {
        // Trước MLACP-307 lời gọi này đi lọt: cổng chặn liệt kê Suspended/Locked, nên Pending —
        // trạng thái mặc định của mọi phòng trà vừa tạo — không nằm trong danh sách bị chặn.
        // Nền tảng nhận tiền vé của khán giả cho một địa điểm mà chính nó chưa bao giờ xác minh.
        var (loungeId, ownerId, _) = await SeedVenueAsync(LoungeStatus.Pending);
        var showId = await SeedDraftShowAsync(loungeId);

        var res = await _factory.CreateAuthenticatedClient(ownerId, "Owner")
            .PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.IsSuccessStatusCode.Should().BeFalse();
        (await res.Content.ReadAsStringAsync()).Should().Contain("chờ Admin duyệt",
            "Owner cần biết mình đang vướng bước duyệt hồ sơ, không phải một lỗi vô danh");
    }

    [Fact]
    public async Task AVenueWhoseApplicationWasRejected_AlsoCannotOpenAShowForSale()
    {
        var (loungeId, ownerId, _) = await SeedVenueAsync(LoungeStatus.Rejected);
        var showId = await SeedDraftShowAsync(loungeId);

        var res = await _factory.CreateAuthenticatedClient(ownerId, "Owner")
            .PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.IsSuccessStatusCode.Should().BeFalse();
        (await res.Content.ReadAsStringAsync()).Should().Contain("bị từ chối");
    }

    [Fact]
    public async Task OnceApproved_TheVenueGateStopsBlocking()
    {
        // Không kỳ vọng nộp duyệt thành công: buổi diễn này chưa có hạng vé, chưa có nghệ sĩ, chưa
        // có văn bản chấp thuận biểu diễn — đó là các cổng chặn khác, và chúng vẫn phải giữ nguyên.
        // Điều cần chốt là cổng TRẠNG THÁI VENUE đã hết chặn: lỗi trả về phải đổi sang chuyện khác.
        var (loungeId, ownerId, _) = await SeedVenueAsync(LoungeStatus.Approved);
        var showId = await SeedDraftShowAsync(loungeId);

        var res = await _factory.CreateAuthenticatedClient(ownerId, "Owner")
            .PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        var body = await res.Content.ReadAsStringAsync();
        body.Should().NotContain("chờ Admin duyệt");
        body.Should().Contain("hạng vé", "cổng kế tiếp mới là cổng được phép chặn ở đây");
    }

    [Fact]
    public async Task AWarnedVenue_KeepsTrading()
    {
        // Cảnh cáo là một vết ghi lại, không phải lệnh dừng — và bảng tổng quan của Admin vẫn đếm
        // venue Warned là đang hoạt động. Ghim lại vì siết cổng chặn rất dễ vô tình kéo luôn Warned
        // vào nhóm bị chặn, và hậu quả là đình chỉ một phòng trà mà không ai định đình chỉ.
        var (loungeId, ownerId, _) = await SeedVenueAsync(LoungeStatus.Warned);
        var showId = await SeedDraftShowAsync(loungeId);

        var res = await _factory.CreateAuthenticatedClient(ownerId, "Owner")
            .PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        (await res.Content.ReadAsStringAsync()).Should().NotContain("chờ Admin duyệt");
    }

    // ---------- hiện diện công khai ----------

    [Fact]
    public async Task AnUnapprovedVenue_IsNotInThePublicList()
    {
        var (_, _, name) = await SeedVenueAsync(LoungeStatus.Pending);

        var res = await Anonymous().GetAsync("/api/v1/lounges?pageSize=50");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().NotContain(name);
    }

    [Fact]
    public async Task ButItsOwnerStillSeesItInTheirOwnList()
    {
        // Giấu hồ sơ đang chờ duyệt khỏi chính người nộp nó thì họ tưởng hồ sơ đã mất.
        var (_, ownerId, name) = await SeedVenueAsync(LoungeStatus.Pending);

        var res = await _factory.CreateAuthenticatedClient(ownerId, "Owner")
            .GetAsync("/api/v1/lounges?mine=true&pageSize=50");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain(name);
    }

    [Fact]
    public async Task AnUnapprovedVenuePage_IsNotReachableByGuessingItsUrl()
    {
        // Lọc khỏi danh sách mà vẫn phục vụ trang chi tiết thì coi như chưa lọc: /lounges/{id} là
        // đường dẫn đoán được.
        var (loungeId, _, _) = await SeedVenueAsync(LoungeStatus.Pending);

        var res = await Anonymous().GetAsync($"/api/v1/lounges/{loungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "với người ngoài, phòng trà chưa duyệt là thứ không tồn tại — trả 403 thì chính câu " +
            "trả lời đó xác nhận có một phòng trà mang Id này");
    }

    [Fact]
    public async Task TheOwnerAndTheAdmin_CanStillOpenThatPage()
    {
        var (loungeId, ownerId, _) = await SeedVenueAsync(LoungeStatus.Pending);

        (await _factory.CreateAuthenticatedClient(ownerId, "Owner")
            .GetAsync($"/api/v1/lounges/{loungeId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Admin().GetAsync($"/api/v1/lounges/{loungeId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnotherOwner_CannotOpenIt()
    {
        var (loungeId, _, _) = await SeedVenueAsync(LoungeStatus.Pending);

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner")
            .GetAsync($"/api/v1/lounges/{loungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- bước duyệt ----------

    [Fact]
    public async Task TheQueue_ShowsWhoIsWaitingAndWhetherTheyAttachedALicence()
    {
        // Giấy phép kinh doanh là căn cứ chính để duyệt, và trường đó không bắt buộc lúc tạo — nên
        // có hồ sơ nộp lên mà không kèm gì để xét. Admin cần thấy điều đó ngay trên danh sách.
        var (withLicence, _, nameWith) = await SeedVenueAsync(
            LoungeStatus.Pending, "https://example.test/licence.pdf");
        var (_, _, nameWithout) = await SeedVenueAsync(LoungeStatus.Pending);

        var res = await Admin().GetAsync("/api/v1/admin/venues/pending?pageSize=50");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<Envelope<Paged<QueueItem>>>();
        body!.Data.Items.Should().Contain(v => v.Name == nameWith && v.HasBusinessLicense);
        body.Data.Items.Should().Contain(v => v.Name == nameWithout && !v.HasBusinessLicense);
        body.Data.Items.Single(v => v.LoungeId == withLicence).OwnerEmail.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ApprovingAVenue_LetsItTradeAndTellsTheOwnerSo()
    {
        var (loungeId, ownerId, _) = await SeedVenueAsync(LoungeStatus.Pending);

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/venues/{loungeId}/review",
            new { Decision = "Approved", ReviewNote = "Giấy phép hợp lệ" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(loungeId)).Should().Be(LoungeStatus.Approved);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.CountAsync(
            n => n.UserId == ownerId && n.Type == NotificationType.VenueReviewResult))
            .Should().Be(1, "Owner không có đường nào khác để biết kết quả");

        var lounge = await db.Set<MusicLoungeVenue>().AsNoTracking().SingleAsync(l => l.Id == loungeId);
        lounge.StatusReviewedBy.Should().Be(SeedHelper.AdminId);
        lounge.StatusReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RejectingWithoutSayingWhy_IsRefused()
    {
        // "Bị từ chối" mà không kèm lý do là bắt Owner đoán xem hồ sơ thiếu gì.
        var (loungeId, _, _) = await SeedVenueAsync(LoungeStatus.Pending);

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/venues/{loungeId}/review",
            new { Decision = "Rejected", ReviewNote = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("lý do");
        (await StatusOfAsync(loungeId)).Should().Be(LoungeStatus.Pending,
            "một quyết định bị chặn phải để nguyên trạng thái đang có");
    }

    [Fact]
    public async Task RejectingWithAReason_RecordsItAndPassesItOn()
    {
        var (loungeId, ownerId, _) = await SeedVenueAsync(LoungeStatus.Pending);
        const string reason = "Ảnh giấy phép kinh doanh bị mờ, không đọc được số";

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/venues/{loungeId}/review",
            new { Decision = "Rejected", ReviewNote = reason });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(loungeId)).Should().Be(LoungeStatus.Rejected);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var note = await db.Notifications.AsNoTracking()
            .SingleAsync(n => n.UserId == ownerId && n.Type == NotificationType.VenueReviewResult);
        note.Body.Should().Contain(reason, "lý do là thứ duy nhất giúp họ sửa và nộp lại");
    }

    [Fact]
    public async Task ARejectedVenue_CanBeApprovedAfterTheOwnerFixesIt()
    {
        var (loungeId, _, _) = await SeedVenueAsync(LoungeStatus.Rejected);

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/venues/{loungeId}/review",
            new { Decision = "Approved", ReviewNote = "Đã nộp lại giấy phép rõ nét" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(loungeId)).Should().Be(LoungeStatus.Approved);
    }

    // ---------- không được thành đường vòng gỡ án phạt ----------

    [Theory]
    [InlineData(LoungeStatus.Suspended)]
    [InlineData(LoungeStatus.Locked)]
    [InlineData(LoungeStatus.Warned)]
    public async Task ReviewingAVenueThatIsUnderAPenalty_IsRefused(LoungeStatus status)
    {
        // Nếu bước duyệt hồ sơ đặt được một venue đang bị đình chỉ về Approved thì nó trở thành
        // đường vòng gỡ án phạt: bỏ qua cả luồng khiếu nại lẫn job hết hạn tạm khoá.
        var (loungeId, _, _) = await SeedVenueAsync(status);

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/venues/{loungeId}/review",
            new { Decision = "Approved", ReviewNote = "Bỏ qua án phạt" });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await StatusOfAsync(loungeId)).Should().Be(status, "án phạt phải còn nguyên");
    }

    [Fact]
    public async Task ApprovingAVenueTwice_IsRefused()
    {
        var (loungeId, ownerId, _) = await SeedVenueAsync(LoungeStatus.Approved);

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/venues/{loungeId}/review",
            new { Decision = "Approved", ReviewNote = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.CountAsync(
            n => n.UserId == ownerId && n.Type == NotificationType.VenueReviewResult))
            .Should().Be(0, "duyệt lại lần hai không được gửi thêm một thông báo nữa");
    }

    [Fact]
    public async Task OnlyAdminsCanReview()
    {
        var (loungeId, ownerId, _) = await SeedVenueAsync(LoungeStatus.Pending);

        var res = await _factory.CreateAuthenticatedClient(ownerId, "Owner")
            .PostAsJsonAsync($"/api/v1/admin/venues/{loungeId}/review",
                new { Decision = "Approved", ReviewNote = "Tự duyệt cho mình" });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await StatusOfAsync(loungeId)).Should().Be(LoungeStatus.Pending);
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
    private sealed record QueueItem(
        int LoungeId, string Name, string Status, string OwnerEmail, bool HasBusinessLicense);
}

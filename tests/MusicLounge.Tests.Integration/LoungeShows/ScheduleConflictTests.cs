using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// CF1, MLACP-308.
///
/// Một phòng trà có một sân khấu, nên nó không chạy được hai buổi diễn chồng giờ nhau. Không có
/// chỗ nào trong hệ thống kiểm điều đó: ba đường ghi lịch (tạo mới, sửa bản nháp, đổi lịch) và
/// đường nộp duyệt đều đặt được giờ diễn mà không ai hỏi giờ đó đã có ai giữ chưa.
///
/// Hậu quả không dừng ở một bảng lịch xấu. Hai buổi diễn cùng khung giờ đều mở bán vé thật, và đến
/// đúng tối đó thì một trong hai bên khán giả buộc phải bị huỷ và hoàn tiền — hệ thống bán một thứ
/// mà nó biết chắc không giao được, chỉ vì chưa ai dạy nó cách biết.
///
/// Mỗi test ở đây dựng phòng trà riêng: quy tắc này chỉ xét trong phạm vi một phòng trà, nên dùng
/// chung venue với bộ test khác thì vừa nhiễu vừa không chứng minh được điều đang cần chứng minh.
/// </summary>
[Collection("Integration")]
public sealed class ScheduleConflictTests
{
    private readonly ApiFactory _factory;

    public ScheduleConflictTests(ApiFactory factory) => _factory = factory;

    private HttpClient Owner(int loungeId)
        => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", loungeId);

    /// <summary>
    /// Phòng trà riêng, chủ là SeedHelper.OwnerId để thừa hưởng gói subscription đang chạy của tài
    /// khoản đó (tạo buổi diễn đòi có gói), kèm tài khoản nhận tiền mặc định (nộp duyệt đòi có).
    /// </summary>
    private async Task<int> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = SeedHelper.OwnerId,
            Name = $"SchedVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Lịch Diễn", Ward = "P1", District = "Q1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id,
            BankName = "Test Bank",
            AccountNumber = scope.ServiceProvider
                .GetRequiredService<IPiiEncryptionService>().Encrypt("0000008888"),
            AccountHolder = "Test Lounge Owner",
            IsDefault = true, IsVerified = true
        });
        await db.SaveChangesAsync();

        return lounge.Id;
    }

    /// <summary>Đặt sẵn một buổi diễn đang giữ chỗ, ghi thẳng vào DB để kiểm soát chính xác khung giờ.</summary>
    private async Task<int> OccupyAsync(
        int loungeId, DateTimeOffset start, DateTimeOffset? end,
        LoungeShowStatus status = LoungeShowStatus.Published, string name = "Buổi diễn đã có")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = loungeId,
            Name = name,
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = status,
            ScheduledStart = start,
            ScheduledEnd = end
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    private Task<HttpResponseMessage> CreateAsync(
        int loungeId, DateTimeOffset start, DateTimeOffset? end)
        => Owner(loungeId).PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = loungeId,
            Name = $"Show-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = "Offline",
            ScheduledStart = start,
            ScheduledEnd = end,
            CategoryId = (int?)null,
            OfflineQuota = 100,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            Performances = new[]
            {
                new
                {
                    PerformerId = (int?)null, PerformerName = "DJ Test", Role = "Main",
                    OrderIndex = 1, SetTime = (string?)null, AcceptsDonation = true
                }
            }
        });

    private static DateTimeOffset Base() => SeedHelper.NextShowStart();

    // ---------- tạo mới ----------

    [Fact]
    public async Task TwoShowsInTheSameSlot_AreRefused()
    {
        // Nếu lời gọi thứ hai này đi lọt thì cả hai buổi diễn đều lên sàn bán vé, và tối đó phòng
        // trà chỉ tổ chức được một.
        var loungeId = await VenueAsync();
        var start = Base();
        await OccupyAsync(loungeId, start, start.AddHours(3));

        var res = await CreateAsync(loungeId, start.AddHours(1), start.AddHours(4));

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Buổi diễn đã có",
            "thông báo phải chỉ ra buổi diễn nào đang chiếm chỗ, nếu không Owner phải tự đi dò");
    }

    [Fact]
    public async Task TheRefusal_SaysWhichHoursAreTaken()
    {
        // Owner đang cầm lịch của chính họ; nói "trùng giờ" mà không nói giờ nào thì họ vẫn phải
        // mở từng buổi diễn ra dò.
        var loungeId = await VenueAsync();
        var start = Base();
        await OccupyAsync(loungeId, start, start.AddHours(3));

        var res = await CreateAsync(loungeId, start.AddHours(1), start.AddHours(4));

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain(start.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm"),
            "giờ Việt Nam, vì người đọc đang đứng ở phòng trà đó chứ không ở múi giờ UTC");
    }

    [Fact]
    public async Task TwoShowsBackToBack_AreAllowed()
    {
        // Chạy hai suất một tối là cách xếp lịch bình thường của phòng trà. Suất sau bắt đầu đúng
        // lúc suất trước kết thúc thì không phải là trùng.
        var loungeId = await VenueAsync();
        var start = Base();
        await OccupyAsync(loungeId, start, start.AddHours(3));

        var res = await CreateAsync(loungeId, start.AddHours(3), start.AddHours(6));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task TheSameSlotAtADifferentVenue_IsFine()
    {
        // Ràng buộc là "một sân khấu không diễn hai chỗ cùng lúc", không phải "cả nền tảng chỉ
        // được có một buổi diễn tại một thời điểm".
        var first = await VenueAsync();
        var second = await VenueAsync();
        var start = Base();
        await OccupyAsync(first, start, start.AddHours(3));

        var res = await CreateAsync(second, start, start.AddHours(3));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ADraftDoesNotHoldTheSlot()
    {
        // Bản nháp là chỗ Owner dựng thử, và dựng hai phương án cho cùng một buổi tối là việc bình
        // thường. Chỉ khi nộp duyệt thì buổi diễn mới thật sự đặt chỗ.
        var loungeId = await VenueAsync();
        var start = Base();
        await OccupyAsync(loungeId, start, start.AddHours(3), LoungeShowStatus.Draft);

        var res = await CreateAsync(loungeId, start, start.AddHours(3));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(LoungeShowStatus.Cancelled)]
    [InlineData(LoungeShowStatus.Ended)]
    public async Task AShowThatIsOverOrCalledOff_ReleasesTheSlot(LoungeShowStatus status)
    {
        var loungeId = await VenueAsync();
        var start = Base();
        await OccupyAsync(loungeId, start, start.AddHours(3), status);

        var res = await CreateAsync(loungeId, start, start.AddHours(3));

        res.StatusCode.Should().Be(HttpStatusCode.Created,
            "buổi diễn đã huỷ hoặc đã diễn xong thì đã trả chỗ lại rồi");
    }

    [Fact]
    public async Task AShowWithNoEndTime_StillHoldsItsFourHours()
    {
        // ScheduledEnd cho phép null, và cả hệ thống từ lâu đã hiểu "không khai giờ kết thúc" là 4
        // tiếng — hạn hoàn tiền, lịch đối soát, job tự kết thúc buổi diễn đều tính như vậy. Bộ
        // chống trùng lịch phải hiểu y hệt, nếu không nó vừa chặn nhầm vừa bỏ lọt.
        var loungeId = await VenueAsync();
        var start = Base();
        await OccupyAsync(loungeId, start, null);

        var inside = await CreateAsync(loungeId, start.AddHours(3), start.AddHours(5));
        inside.StatusCode.Should().Be(HttpStatusCode.Conflict, "3 tiếng sau vẫn nằm trong 4 tiếng");

        var after = await CreateAsync(loungeId, start.AddHours(4), start.AddHours(6));
        after.StatusCode.Should().Be(HttpStatusCode.Created, "đúng 4 tiếng sau là đã hết giờ giữ chỗ");
    }

    // ---------- sửa bản nháp ----------

    [Fact]
    public async Task EditingADraftIntoAnOccupiedSlot_IsRefused()
    {
        var loungeId = await VenueAsync();
        var taken = Base();
        await OccupyAsync(loungeId, taken, taken.AddHours(3));

        var free = Base();
        var create = await CreateAsync(loungeId, free, free.AddHours(2));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var showId = (await create.Content.ReadFromJsonAsync<Envelope<int>>())!.Data;

        var res = await Owner(loungeId).PutAsJsonAsync($"/api/v1/lounge-shows/{showId}", new
        {
            Name = "Đổi giờ",
            Description = "Integration test show",
            ScheduledStart = taken.AddHours(1),
            ScheduledEnd = taken.AddHours(2),
            CategoryId = (int?)null,
            OfflineQuota = 100,
            OnlineQuota = (int?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EditingADraftWithoutMovingIt_IsNotAConflictWithItself()
    {
        // Buổi diễn phải tự loại mình ra khỏi phép so, nếu không mọi lần sửa đều tự đụng chính nó.
        var loungeId = await VenueAsync();
        var start = Base();
        var create = await CreateAsync(loungeId, start, start.AddHours(2));
        var showId = (await create.Content.ReadFromJsonAsync<Envelope<int>>())!.Data;

        var res = await Owner(loungeId).PutAsJsonAsync($"/api/v1/lounge-shows/{showId}", new
        {
            Name = "Đổi tên thôi",
            Description = "Integration test show",
            ScheduledStart = start,
            ScheduledEnd = start.AddHours(2),
            CategoryId = (int?)null,
            OfflineQuota = 120,
            OnlineQuota = (int?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---------- nộp duyệt ----------

    [Fact]
    public async Task PublishingIntoASlotTakenSinceTheDraftWasWritten_IsRefused()
    {
        // Kiểm lúc tạo là để báo sớm; kiểm lúc nộp duyệt mới là chốt. Giữa hai thời điểm đó một
        // buổi diễn khác hoàn toàn có thể đã chiếm mất khung giờ — đây đúng là tình huống đó.
        var loungeId = await VenueAsync();
        var start = Base();

        var create = await CreateAsync(loungeId, start, start.AddHours(2));
        create.StatusCode.Should().Be(HttpStatusCode.Created, "lúc tạo thì khung giờ còn trống");
        var showId = (await create.Content.ReadFromJsonAsync<Envelope<int>>())!.Data;

        await OccupyAsync(loungeId, start, start.AddHours(2), name: "Buổi diễn chen ngang");

        var res = await Owner(loungeId).PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Buổi diễn chen ngang");
    }

    [Fact]
    public async Task PublishingIntoAFreeSlot_GetsPastTheScheduleCheck()
    {
        // Không kỳ vọng nộp duyệt thành công: buổi diễn này chưa có hạng vé — đó là cổng chặn khác
        // và nó phải giữ nguyên. Điều cần chốt là cổng LỊCH DIỄN không chặn, tức lỗi phải đổi sang
        // chuyện khác chứ không còn là trùng giờ.
        var loungeId = await VenueAsync();
        var start = Base();
        var create = await CreateAsync(loungeId, start, start.AddHours(2));
        var showId = (await create.Content.ReadFromJsonAsync<Envelope<int>>())!.Data;

        var res = await Owner(loungeId).PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.StatusCode.Should().NotBe(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).Should().Contain("hạng vé");
    }

    // ---------- đổi lịch ----------

    [Fact]
    public async Task ReschedulingOntoAnotherShow_IsRefused()
    {
        var loungeId = await VenueAsync();
        var taken = Base();
        await OccupyAsync(loungeId, taken, taken.AddHours(3));

        var mine = Base();
        var showId = await OccupyAsync(loungeId, mine, mine.AddHours(2), name: "Buổi diễn của tôi");

        var res = await Owner(loungeId).PostAsJsonAsync(
            $"/api/v1/lounge-shows/{showId}/reschedule",
            new { NewScheduledStart = taken.AddHours(1) });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloaded = await db.LoungeShows.AsNoTracking().SingleAsync(s => s.Id == showId);
        reloaded.ScheduledStart.Should().BeCloseTo(mine, TimeSpan.FromSeconds(1),
            "lịch bị từ chối thì không được dời đi đâu cả");
    }

    [Fact]
    public async Task ReschedulingIntoAFreeSlot_Works()
    {
        var loungeId = await VenueAsync();
        var mine = Base();
        var showId = await OccupyAsync(loungeId, mine, mine.AddHours(2), name: "Buổi diễn của tôi");
        var target = Base();

        var res = await Owner(loungeId).PostAsJsonAsync(
            $"/api/v1/lounge-shows/{showId}/reschedule",
            new { NewScheduledStart = target });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed record Envelope<T>(bool Success, T Data);
}

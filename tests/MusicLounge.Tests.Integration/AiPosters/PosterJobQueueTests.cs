using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Settings;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.AiPosters;

/// <summary>
/// MLACP-458. Google Flow chỉ nhận lệnh phát ra từ một tab trình duyệt đã đăng nhập, nên máy chủ trên Azure không gọi
/// thẳng được và một lượt sinh ảnh mất 50–90 giây. Đường tạo poster vì vậy đổi sang: ghi đơn → máy trạm bên ngoài đến
/// nhận → nộp ảnh về. Bộ test này đi trọn đường đó, kể cả các đường hỏng (máy trạm chết giữa chừng, không ai trực).
/// </summary>
[Collection("Integration")]
public sealed class PosterJobQueueTests
{
    private const string KhoaMayTram = "test-poster-worker-key";
    private readonly ApiFactory _factory;

    public PosterJobQueueTests(ApiFactory factory) => _factory = factory;

    /// <summary>Nhà cung cấp chế độ hàng đợi: không sinh ảnh, chỉ khai báo ảnh sẽ tới muộn.</summary>
    private sealed class NhaCungCapHangDoi : IAiImageGenerationService
    {
        public bool IsDeferred => true;
        public string ProviderName => "flow";
        public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken ct = default)
            => throw new InvalidOperationException("không bao giờ được gọi ở chế độ hàng đợi");
    }

    // Không dispose: Program.cs gọi Log.CloseAndFlush() khi host tắt — huỷ giữa phiên sẽ tắt nhật ký của test khác.
    private WebApplicationFactory<Program> CheDoHangDoi() =>
        _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.Replace(ServiceDescriptor.Scoped<IAiImageGenerationService>(_ => new NhaCungCapHangDoi()));
            s.Configure<PosterWorkerSettings>(o => { o.Enabled = true; o.ApiKey = KhoaMayTram; });
        }));

    private static byte[] AnhPng() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private static HttpClient MayTram(WebApplicationFactory<Program> factory, string? khoa = KhoaMayTram)
    {
        var client = factory.CreateClient();
        if (khoa is not null) client.DefaultRequestHeaders.Add("X-Poster-Worker-Key", khoa);
        return client;
    }

    private static HttpClient ChuPhongTra(WebApplicationFactory<Program> factory, int ownerId, int loungeId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderUserId, ownerId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRole, "Owner");
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderLoungeId, loungeId.ToString());
        return client;
    }

    /// <summary>
    /// Chủ phòng trà + phòng trà + gói dịch vụ RIÊNG cho mỗi bài test. Không đụng dữ liệu mẫu dùng chung: hạn mức poster
    /// của chủ phòng trà mẫu là 10 và nhiều test khác đang dựa vào đó.
    /// </summary>
    private async Task<(int OwnerId, int LoungeId)> ChuPhongTraRiengAsync(int hanMucThang)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User
        {
            Email = $"poster-owner-{Guid.NewGuid():N}@test.com", FullName = "Chu phong tra poster",
            Role = UserRole.Owner
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"PosterVenue-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();

        db.OwnerSubscriptions.Add(new OwnerSubscription
        {
            OwnerId = owner.Id, PackageId = 1,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(29),
            Status = SubscriptionStatus.Active,
            MaxTicketsPerEventSnapshot = 1000, HasAiPosterSnapshot = true,
            MaxAiPostersPerMonthSnapshot = hanMucThang, MaxTourScenesSnapshot = 5
        });
        await db.SaveChangesAsync();

        return (owner.Id, lounge.Id);
    }

    private async Task<int> TaoBuoiHoaNhacAsync(HttpClient client, int loungeId)
    {
        var res = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = loungeId,
            Name = $"PosterQueue-{Guid.NewGuid():N}",
            Description = "test",
            Format = "Offline",
            ScheduledStart = SeedHelper.NextShowStart(),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = 100,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            Performances = Array.Empty<object>()
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<IdResponse>())!.Data;
    }

    /// <summary>
    /// Dọn sạch hàng đợi trước khi bài test đặt đơn của mình.
    ///
    /// Hàng đợi là TOÀN CỤC và phục vụ theo thứ tự đến trước: nếu một bài test trước đó để lại đơn chưa xử lý thì lệnh
    /// "nhận đơn" của bài này sẽ bốc trúng đơn của người khác — và bài test sẽ hỏng vì một lý do chẳng liên quan gì tới
    /// thứ nó đang kiểm. (Đúng lỗi này đã làm 3 bài đỏ lúc chạy lần đầu.)
    /// </summary>
    private static async Task DonSachHangDoiAsync(WebApplicationFactory<Program> factory)
    {
        while (true)
        {
            var res = await MayTram(factory).PostAsJsonAsync("/api/v1/poster-jobs/claim", new { WorkerId = "may-don-dep" });
            if (res.StatusCode != HttpStatusCode.OK) return;

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var id = doc.RootElement.GetProperty("data").GetProperty("id").GetInt32();
            await MayTram(factory).PostAsJsonAsync(
                $"/api/v1/poster-jobs/{id}/fail", new { WorkerId = "may-don-dep", Reason = "don dep hang doi truoc khi test" });
        }
    }

    private static async Task<(int Id, int ShowId, string Prompt)> NhanDonAsync(HttpClient mayTram, string workerId = "may-1")
    {
        var res = await mayTram.PostAsJsonAsync("/api/v1/poster-jobs/claim", new { WorkerId = workerId });
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        return (data.GetProperty("id").GetInt32(), data.GetProperty("showId").GetInt32(),
            data.GetProperty("prompt").GetString()!);
    }

    private static async Task<HttpResponseMessage> NopAnhAsync(
        HttpClient mayTram, int jobId, byte[] anh, string workerId = "may-1")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(anh);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "poster.png");
        form.Add(new StringContent(workerId), "workerId");
        return await mayTram.PostAsync($"/api/v1/poster-jobs/{jobId}/result", form);
    }

    private async Task<AiPosterGeneration> DonAsync(int jobId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<AiPosterGeneration>().AsNoTracking().SingleAsync(g => g.Id == jobId);
    }

    // ---------- đường đi đúng ----------

    [Fact]
    public async Task BamTaoPoster_CheDoHangDoi_Tra202VaGhiDonCho()
    {
        var factory = CheDoHangDoi();
        await DonSachHangDoiAsync(factory);
        var (ownerId, loungeId) = await ChuPhongTraRiengAsync(hanMucThang: 10);
        var chu = ChuPhongTra(factory, ownerId, loungeId);
        var showId = await TaoBuoiHoaNhacAsync(chu, loungeId);

        var res = await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });

        res.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "ảnh chưa có — giữ người dùng chờ 50–90 giây là thiết kế sai");
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("status").GetString().Should().Be("Queued");
        data.GetProperty("attemptId").GetInt32().Should().BeGreaterThan(0, "giao diện cần mã đơn để hỏi lại trạng thái");

        var don = await DonAsync(data.GetProperty("attemptId").GetInt32());
        don.Status.Should().Be(AiPosterGenerationStatus.Queued);
        don.ShowId.Should().Be(showId);
        don.Provider.Should().Be("flow");
        don.Prompt.Should().Contain("KHÔNG chứa chữ",
            "chữ tiếng Việt in bằng font ở bước sau — để mô hình tự vẽ chữ là sai dấu");
    }

    [Fact]
    public async Task MayTramNhanDon_ThiDonDuocKhoaLaiBangHanThue()
    {
        var factory = CheDoHangDoi();
        await DonSachHangDoiAsync(factory);
        var (ownerId, loungeId) = await ChuPhongTraRiengAsync(10);
        var chu = ChuPhongTra(factory, ownerId, loungeId);
        var showId = await TaoBuoiHoaNhacAsync(chu, loungeId);
        await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });

        var don = await NhanDonAsync(MayTram(factory));

        don.ShowId.Should().Be(showId);
        don.Prompt.Should().NotBeEmpty("máy trạm cần chính lời nhắc để gọi Google Flow");

        var luu = await DonAsync(don.Id);
        luu.Status.Should().Be(AiPosterGenerationStatus.Rendering);
        luu.ClaimedBy.Should().Be("may-1");
        luu.LeaseExpiresAt.Should().NotBeNull("không có hạn thuê thì đơn kẹt mãi khi máy trạm chết");
        luu.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task MayTramNopAnh_ThiPosterGanVaoBuoiHoaNhac_VaChuPhongTraDuocBao()
    {
        var factory = CheDoHangDoi();
        await DonSachHangDoiAsync(factory);
        var (ownerId, loungeId) = await ChuPhongTraRiengAsync(10);
        var chu = ChuPhongTra(factory, ownerId, loungeId);
        var showId = await TaoBuoiHoaNhacAsync(chu, loungeId);
        await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });
        var don = await NhanDonAsync(MayTram(factory));

        var res = await NopAnhAsync(MayTram(factory), don.Id, AnhPng());

        res.StatusCode.Should().Be(HttpStatusCode.NoContent, await res.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var luu = await db.Set<AiPosterGeneration>().AsNoTracking().SingleAsync(g => g.Id == don.Id);
        luu.Status.Should().Be(AiPosterGenerationStatus.Succeeded);
        luu.ImageUrl.Should().NotBeNullOrEmpty();
        luu.LeaseExpiresAt.Should().BeNull("đơn xong rồi thì không còn hạn thuê nào để hết hạn");

        var show = await db.LoungeShows.AsNoTracking().SingleAsync(s => s.Id == showId);
        show.PosterUrl.Should().Be(luu.ImageUrl);
        show.PosterByAi.Should().BeTrue();

        var thongBao = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == ownerId && n.Type == NotificationType.PosterGenerationResult)
            .ToListAsync();
        thongBao.Should().ContainSingle("chủ phòng trà đã rời màn hình từ lâu — thông báo là đường duy nhất họ biết kết quả");
    }

    // ---------- hạn mức ----------

    [Fact]
    public async Task DonDangCho_CungTinhVaoHanMucThang()
    {
        var factory = CheDoHangDoi();
        await DonSachHangDoiAsync(factory);
        var (ownerId, loungeId) = await ChuPhongTraRiengAsync(hanMucThang: 1);
        var chu = ChuPhongTra(factory, ownerId, loungeId);
        var show1 = await TaoBuoiHoaNhacAsync(chu, loungeId);
        var show2 = await TaoBuoiHoaNhacAsync(chu, loungeId);

        (await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{show1}/ai-poster", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        var res2 = await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{show2}/ai-poster", new { });

        res2.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "đơn đang chờ phải giữ chỗ — nếu chỉ đếm lần thành công thì còn 1 lượt vẫn bấm được 10 lần");
    }

    [Fact]
    public async Task DonHong_TraLaiLuotChoChuPhongTra()
    {
        var factory = CheDoHangDoi();
        await DonSachHangDoiAsync(factory);
        var (ownerId, loungeId) = await ChuPhongTraRiengAsync(hanMucThang: 1);
        var chu = ChuPhongTra(factory, ownerId, loungeId);
        var show1 = await TaoBuoiHoaNhacAsync(chu, loungeId);
        var show2 = await TaoBuoiHoaNhacAsync(chu, loungeId);
        await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{show1}/ai-poster", new { });
        var don = await NhanDonAsync(MayTram(factory));

        var baoLoi = await MayTram(factory).PostAsJsonAsync(
            $"/api/v1/poster-jobs/{don.Id}/fail", new { WorkerId = "may-1", Reason = "Flow tu choi loi nhac" });
        baoLoi.StatusCode.Should().Be(HttpStatusCode.NoContent, await baoLoi.Content.ReadAsStringAsync());

        (await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{show2}/ai-poster", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted,
                "lỗi của nhà cung cấp thì không được tính vào tiền người ta đã trả (MLACP-419)");
    }

    [Fact]
    public async Task MotBuoiHoaNhac_ChiCoMotDonDangCho()
    {
        var factory = CheDoHangDoi();
        await DonSachHangDoiAsync(factory);
        var (ownerId, loungeId) = await ChuPhongTraRiengAsync(10);
        var chu = ChuPhongTra(factory, ownerId, loungeId);
        var showId = await TaoBuoiHoaNhacAsync(chu, loungeId);
        await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });

        var lanHai = await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });

        lanHai.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "bấm lại trong lúc chờ là vì tưởng lần trước chưa ăn, không phải muốn hai poster — mỗi đơn là một lượt hạn mức Google thật");
    }

    // ---------- an ninh và đường hỏng ----------

    [Fact]
    public async Task KhongCoKhoaMayTram_ThiKhongGoiDuocEndpointNao()
    {
        var factory = CheDoHangDoi();

        var khongKhoa = await MayTram(factory, khoa: null).PostAsJsonAsync("/api/v1/poster-jobs/claim", new { WorkerId = "x" });
        var saiKhoa = await MayTram(factory, khoa: "sai-khoa").PostAsJsonAsync("/api/v1/poster-jobs/claim", new { WorkerId = "x" });

        khongKhoa.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        saiKhoa.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HangDoiRong_ThiTra204_KhongPhaiLoi()
    {
        var factory = CheDoHangDoi();
        await DonSachHangDoiAsync(factory);

        var res = await MayTram(factory).PostAsJsonAsync("/api/v1/poster-jobs/claim", new { WorkerId = "may-1" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent, "hàng đợi rỗng là chuyện thường gặp nhất, không phải lỗi");
    }

    [Fact]
    public async Task NopDuLieuKhongPhaiAnh_ThiBiTuChoi()
    {
        var factory = CheDoHangDoi();
        await DonSachHangDoiAsync(factory);
        var (ownerId, loungeId) = await ChuPhongTraRiengAsync(10);
        var chu = ChuPhongTra(factory, ownerId, loungeId);
        var showId = await TaoBuoiHoaNhacAsync(chu, loungeId);
        await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });
        var don = await NhanDonAsync(MayTram(factory));

        var res = await NopAnhAsync(MayTram(factory), don.Id, "day khong phai anh"u8.ToArray());

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await DonAsync(don.Id)).Status.Should().Be(AiPosterGenerationStatus.Rendering, "chưa nhận được ảnh thì đơn chưa xong");
    }

    [Fact]
    public async Task MayTramCu_NopBaiSauKhiDonDuocGiaoLai_ThiBiTuChoi()
    {
        var factory = CheDoHangDoi();
        await DonSachHangDoiAsync(factory);
        var (ownerId, loungeId) = await ChuPhongTraRiengAsync(10);
        var chu = ChuPhongTra(factory, ownerId, loungeId);
        var showId = await TaoBuoiHoaNhacAsync(chu, loungeId);
        await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });
        var don = await NhanDonAsync(MayTram(factory), workerId: "may-cu");

        await HetHanThueAsync(don.Id);
        await ChayJobDonDonAsync();
        var donMoi = await NhanDonAsync(MayTram(factory), workerId: "may-moi");
        donMoi.Id.Should().Be(don.Id, "tiền đề: vẫn là đơn cũ, chỉ đổi người làm");

        var res = await NopAnhAsync(MayTram(factory), don.Id, AnhPng(), workerId: "may-cu");

        res.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "hai máy cùng nộp thì poster bị ghi đè theo thứ tự về đích, không theo ý ai cả");
    }

    // ---------- job dọn đơn treo ----------

    [Fact]
    public async Task HetHanThue_ThiDonQuayVeHangDoi()
    {
        var factory = CheDoHangDoi();
        await DonSachHangDoiAsync(factory);
        var (ownerId, loungeId) = await ChuPhongTraRiengAsync(10);
        var chu = ChuPhongTra(factory, ownerId, loungeId);
        var showId = await TaoBuoiHoaNhacAsync(chu, loungeId);
        await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });
        var don = await NhanDonAsync(MayTram(factory));
        await HetHanThueAsync(don.Id);

        await ChayJobDonDonAsync();

        var luu = await DonAsync(don.Id);
        luu.Status.Should().Be(AiPosterGenerationStatus.Queued);
        luu.ClaimedBy.Should().BeNull();
        luu.AttemptCount.Should().Be(1, "số lần thử chỉ tăng khi thực sự giao cho máy trạm");
    }

    [Fact]
    public async Task QuaSoLanThuChoPhep_ThiDongDon_VaBaoChuPhongTra()
    {
        var factory = CheDoHangDoi();
        await DonSachHangDoiAsync(factory);
        var (ownerId, loungeId) = await ChuPhongTraRiengAsync(10);
        var chu = ChuPhongTra(factory, ownerId, loungeId);
        var showId = await TaoBuoiHoaNhacAsync(chu, loungeId);
        await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });

        int jobId = 0;
        for (var i = 0; i < PosterQueue.MaxAttempts; i++)
        {
            jobId = (await NhanDonAsync(MayTram(factory), $"may-{i}")).Id;
            await HetHanThueAsync(jobId);
            await ChayJobDonDonAsync();
        }

        var luu = await DonAsync(jobId);
        luu.Status.Should().Be(AiPosterGenerationStatus.Failed, "thử mãi một đơn luôn làm máy trạm chết chỉ tốn hạn mức Google");
        luu.ErrorMessage.Should().Be(PosterQueue.ThongBaoMayTramKhongPhanHoi);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.AsNoTracking()
                .CountAsync(n => n.UserId == ownerId && n.Type == NotificationType.PosterGenerationResult))
            .Should().Be(1);
    }

    [Fact]
    public async Task DonChoQuaLau_ThiHetHan_ChuKhongTreoMai()
    {
        var factory = CheDoHangDoi();
        await DonSachHangDoiAsync(factory);
        var (ownerId, loungeId) = await ChuPhongTraRiengAsync(10);
        var chu = ChuPhongTra(factory, ownerId, loungeId);
        var showId = await TaoBuoiHoaNhacAsync(chu, loungeId);
        var res = await chu.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var jobId = doc.RootElement.GetProperty("data").GetProperty("attemptId").GetInt32();

        await SuaNgayTaoAsync(jobId, DateTimeOffset.UtcNow - PosterQueue.QueueTimeout - TimeSpan.FromMinutes(1));
        await ChayJobDonDonAsync();

        var luu = await DonAsync(jobId);
        luu.Status.Should().Be(AiPosterGenerationStatus.Expired,
            "không có máy trạm nào trực thì phải nói thật, chứ không để chủ phòng trà chờ một tấm poster không bao giờ tới");
        luu.ErrorMessage.Should().Be(PosterQueue.ThongBaoHetHanCho);
    }

    // ---------- tiện ích ----------

    private async Task HetHanThueAsync(int jobId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var job = await db.Set<AiPosterGeneration>().SingleAsync(g => g.Id == jobId);
        job.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
    }

    private async Task SuaNgayTaoAsync(int jobId, DateTimeOffset createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var job = await db.Set<AiPosterGeneration>().SingleAsync(g => g.Id == jobId);
        job.CreatedAt = createdAt;
        await db.SaveChangesAsync();
    }

    private async Task ChayJobDonDonAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<ExpirePosterJobsJob>();
        await job.ExecuteAsync(new Hangfire.JobCancellationToken(false));
    }

    private sealed record IdResponse(bool Success, int Data);
}

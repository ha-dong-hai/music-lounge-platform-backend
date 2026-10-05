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

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-676. Chủ phòng trà huỷ buổi hòa nhạc đã mở bán phải nêu lý do; buổi diễn bị huỷ và khán giả được hoàn NGAY, còn
/// Admin xét lý do để miễn hay phạt (chủ dự án chốt 06/10/2026 — Admin "từ chối huỷ" không ép được phòng trà biểu diễn).
/// </summary>
[Collection("Integration")]
public sealed class ShowCancellationReviewTests
{
    private const decimal Price = 200_000m;
    private const string MoTa = "Ca sĩ chính bị sốt xuất huyết, bác sĩ yêu cầu nghỉ một tuần, không tìm được người thay.";

    private readonly ApiFactory _factory;

    public ShowCancellationReviewTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(Guid OwnerId, Guid LoungeId);

    private async Task<Venue> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"huy676-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà 676" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"PT676-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        return new Venue(owner.Id, lounge.Id);
    }

    private async Task<Guid> ShowAsync(Guid loungeId, LoungeShowStatus status = LoungeShowStatus.Published)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var start = DateTimeOffset.UtcNow.AddDays(10);
        var show = new LoungeShow
        {
            LoungeId = loungeId, Name = $"Đêm 676 {Guid.NewGuid():N}"[..16], Description = "MLACP-676",
            Format = LoungeShowFormat.Offline, Status = status, ScheduledStart = start, ScheduledEnd = start.AddHours(3),
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    /// <summary>Một vé bán online đã thanh toán cho người mua này.</summary>
    private async Task<Guid> BuyerWithTicketAsync(Guid showId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var buyer = new User { Email = $"mua676-{Guid.NewGuid():N}@test.com", FullName = "Người mua 676" };
        db.Users.Add(buyer);
        var tier = new TicketTier { LoungeShowId = showId, Name = "Vào cửa", AccessType = AccessType.Physical, CreatedAt = DateTime.UtcNow };
        db.Add(tier);
        await db.SaveChangesAsync();
        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Đợt 1", Price = Price, PurchaseChannel = PurchaseChannel.Online,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-30)
        };
        db.Add(price);
        await db.SaveChangesAsync();
        var payment = new Payment
        {
            OrderId = $"M676-{Guid.NewGuid():N}"[..30], PayerId = buyer.Id, GrossAmount = Price, NetAmount = Price,
            Method = PaymentMethod.Gateway, Status = PaymentStatus.Confirmed, ReferenceType = "TicketHold", ReferenceId = "0",
            TransactionId = $"V{Guid.NewGuid():N}"[..16], PaidAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        db.Add(payment);
        await db.SaveChangesAsync();
        db.Add(new Ticket
        {
            Id = Guid.NewGuid(), BuyerId = buyer.Id, PriceId = price.Id, TierId = tier.Id, ShowId = showId,
            PaymentId = payment.Id, Status = TicketStatus.Confirmed, PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();
        return buyer.Id;
    }

    private Task<HttpResponseMessage> CancelAsync(Venue v, Guid showId, object? body)
        => body is null
            ? _factory.CreateAuthenticatedClient(v.OwnerId, "Owner", v.LoungeId).PostAsync($"/api/v1/lounge-shows/{showId}/cancel", null)
            : _factory.CreateAuthenticatedClient(v.OwnerId, "Owner", v.LoungeId).PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/cancel", body);

    private static object LyDo(string reason = "PerformerUnavailable", string detail = MoTa) => new { Reason = reason, Detail = detail };

    private async Task<T> DbAsync<T>(Func<ApplicationDbContext, Task<T>> q)
    {
        using var scope = _factory.Services.CreateScope();
        return await q(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private Task<HttpResponseMessage> DecideAsync(Guid reviewId, object body)
        => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .PostAsJsonAsync($"/api/v1/admin/show-cancellations/{reviewId}/decide", body);

    [Fact]
    public async Task BuoiDaMoBan_ChuHuyKhongCoLyDo_BiTuChoi_VaBuoiVanMoBan()
    {
        var v = await VenueAsync();
        var show = await ShowAsync(v.LoungeId);
        await BuyerWithTicketAsync(show);

        (await CancelAsync(v, show, null)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CancelAsync(v, show, LyDo(detail: "bận"))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "mô tả quá ngắn thì Admin không có gì để xét");
        (await DbAsync(db => db.LoungeShows.AsNoTracking().SingleAsync(s => s.Id == show))).Status
            .Should().Be(LoungeShowStatus.Published);
    }

    [Fact]
    public async Task BuoiNhap_HuyKhongCanLyDo_KhongVaoHangXet()
    {
        var v = await VenueAsync();
        var show = await ShowAsync(v.LoungeId, LoungeShowStatus.Draft);

        (await CancelAsync(v, show, null)).IsSuccessStatusCode.Should().BeTrue("chưa mở bán thì không có ai bị ảnh hưởng");
        (await DbAsync(db => db.ShowCancellationReviews.AnyAsync(r => r.ShowId == show))).Should().BeFalse();
    }

    [Fact]
    public async Task CoLyDo_HuyNgay_KhachDuocBaoLyDoTrungTinh_AdminCoViecXet()
    {
        var v = await VenueAsync();
        var show = await ShowAsync(v.LoungeId);
        var buyer = await BuyerWithTicketAsync(show);

        (await CancelAsync(v, show, LyDo())).IsSuccessStatusCode.Should().BeTrue();

        (await DbAsync(db => db.LoungeShows.AsNoTracking().SingleAsync(s => s.Id == show))).Status
            .Should().Be(LoungeShowStatus.Cancelled, "huỷ có hiệu lực ngay — không chờ Admin");
        var review = await DbAsync(db => db.ShowCancellationReviews.AsNoTracking().SingleAsync(r => r.ShowId == show));
        review.Status.Should().Be(ShowCancellationReviewStatus.Pending);
        review.Reason.Should().Be(ShowCancellationReason.PerformerUnavailable);
        review.TicketsRefunded.Should().Be(1);
        review.AmountRefunded.Should().Be(Price);

        var bao = await DbAsync(db => db.Notifications.AsNoTracking()
            .SingleAsync(n => n.UserId == buyer && n.Type == NotificationType.EventCancelled));
        bao.Body.Should().Contain("vì nghệ sĩ không thể biểu diễn")
            .And.NotContain("sốt xuất huyết", "mô tả chi tiết là chuyện giữa phòng trà và nền tảng");

        (await DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == SeedHelper.AdminId
                && n.Type == NotificationType.ShowCancellationReview && n.ReferenceId == review.Id.ToString())))
            .Should().BeTrue("Admin phải biết có lý do cần xét");

        var ds = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetFromJsonAsync<ListResponse>("/api/v1/admin/show-cancellations");
        ds!.Data.Items.Should().Contain(i => i.Id == review.Id && i.ReasonLabel == "Nghệ sĩ không thể biểu diễn" && i.Detail == MoTa);
    }

    private sealed record Item(Guid Id, string ReasonLabel, string Detail, int EarlierCancellations);
    private sealed record Page(List<Item> Items);
    private sealed record ListResponse(Page Data);

    [Fact]
    public async Task AdminPhat_RaAnCanhCao_KhongXetLaiDuoc()
    {
        var v = await VenueAsync();
        var show = await ShowAsync(v.LoungeId);
        await BuyerWithTicketAsync(show);
        await CancelAsync(v, show, LyDo("LowSales", "Bán được ít vé quá, chỉ 12 vé trên 90 chỗ nên không đủ chi phí."));
        var review = await DbAsync(db => db.ShowCancellationReviews.AsNoTracking().SingleAsync(r => r.ShowId == show));

        (await DecideAsync(review.Id, new { Decision = "Penalize", Note = "Bán ít vé không phải lý do huỷ chính đáng." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var sau = await DbAsync(db => db.ShowCancellationReviews.AsNoTracking().SingleAsync(r => r.Id == review.Id));
        sau.Status.Should().Be(ShowCancellationReviewStatus.Penalized);
        var an = await DbAsync(db => db.VenuePenalties.AsNoTracking().SingleAsync(p => p.Id == sau.PenaltyId));
        an.PenaltyType.Should().Be(PenaltyType.Warning, "mặc định là cảnh cáo");
        an.LoungeId.Should().Be(v.LoungeId);
        an.EvidenceRef.Should().Be($"show-cancellation-review:{review.Id}");
        (await DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == v.OwnerId && n.Type == NotificationType.PenaltyIssued)))
            .Should().BeTrue();

        (await DecideAsync(review.Id, new { Decision = "Excuse", Note = "Đổi ý" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "một lần huỷ chỉ xét một lần — không ra án hai lần");
    }

    [Fact]
    public async Task AdminMienPhat_KhongCoAn_ChuDuocBao()
    {
        var v = await VenueAsync();
        var show = await ShowAsync(v.LoungeId);
        await BuyerWithTicketAsync(show);
        await CancelAsync(v, show, LyDo("AuthorityRequest", "Phường yêu cầu tạm dừng biểu diễn để sửa lối thoát hiểm theo biên bản kiểm tra PCCC."));
        var review = await DbAsync(db => db.ShowCancellationReviews.AsNoTracking().SingleAsync(r => r.ShowId == show));

        (await DecideAsync(review.Id, new { Decision = "Excuse", Note = "Có biên bản của cơ quan chức năng." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await DbAsync(db => db.ShowCancellationReviews.AsNoTracking().SingleAsync(r => r.Id == review.Id))).Status
            .Should().Be(ShowCancellationReviewStatus.Excused);
        (await DbAsync(db => db.VenuePenalties.AnyAsync(p => p.LoungeId == v.LoungeId))).Should().BeFalse();
        (await DbAsync(db => db.Notifications.AsNoTracking().SingleAsync(n =>
                n.UserId == v.OwnerId && n.Type == NotificationType.ShowCancellationReview)))
            .Title.Should().Contain("được chấp nhận");
    }
}

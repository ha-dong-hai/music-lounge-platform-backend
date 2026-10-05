using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Admin;

/// <summary>
/// MLACP-672. Chủ dự án phản hồi 05/10: "nhiều màn hình của các role tự nhiên lấy id ra làm gì mà đáng lẽ phải là thông
/// tin liên quan". Đo trên web đã deploy: khiếu nại in "Buổi diễn #01A10C82", hoàn tiền in "Yêu cầu #0CAD7D02 · thanh
/// toán #0A35AD2C", đơn gọi món in "#00000008" — vì DTO không trả tên nào khác ngoài mã. Mỗi bài dựng dữ liệu riêng.
/// </summary>
[Collection("Integration")]
public sealed class NamesInsteadOfIdsTests
{
    private readonly ApiFactory _factory;

    public NamesInsteadOfIdsTests(ApiFactory factory) => _factory = factory;

    private async Task<T> InDbAsync<T>(Func<ApplicationDbContext, Task<T>> f)
    {
        using var scope = _factory.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private Task<string> ShowNameAsync() => InDbAsync(db => db.LoungeShows.Where(s => s.Id == SeedHelper.ShowId).Select(s => s.Name).SingleAsync());

    private static async Task<JsonElement> ItemAsync(HttpResponseMessage res, Guid id)
    {
        res.EnsureSuccessStatusCode();
        var data = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var items = data.ValueKind == JsonValueKind.Array ? data : data.GetProperty("items");
        return items.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task AComplaint_NamesWhatItIsAbout()
    {
        var tu = $"ten{Guid.NewGuid():N}"[..20];
        var id = await InDbAsync(async db =>
        {
            var c = new Complaint
            {
                ComplainantUserId = SeedHelper.AudienceId, TargetType = "show", TargetId = SeedHelper.ShowId,
                Category = ComplaintCategory.Other, Description = tu, Status = ComplaintStatus.Open,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(c); await db.SaveChangesAsync(); return c.Id;
        });

        var row = await ItemAsync(await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetAsync($"/api/v1/admin/complaints?status=Open&keyword={tu}&pageSize=10"), id);

        row.GetProperty("targetName").GetString().Should().Be(await ShowNameAsync());
    }

    [Fact]
    public async Task APendingModeration_NamesTheShow()
    {
        var id = await InDbAsync(async db =>
        {
            var m = new EventModeration
            {
                TargetType = ModerationTargetType.Show, TargetId = SeedHelper.ShowId,
                CreatedAt = DateTime.UtcNow, SlaDeadline = DateTimeOffset.UtcNow.AddDays(1)
            };
            db.Add(m); await db.SaveChangesAsync(); return m.Id;
        });
        try
        {
            var row = await ItemAsync(await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
                .GetAsync($"/api/v1/moderations/pending?targetType=Show&targetId={SeedHelper.ShowId}&pageSize=100"), id);

            row.GetProperty("targetName").GetString().Should().Be(await ShowNameAsync());
        }
        finally
        {
            // Hàng duyệt dùng chung giữa các test: không để lại một mục "đang chờ".
            await InDbAsync(async db => { db.Remove(await db.Set<EventModeration>().SingleAsync(x => x.Id == id)); return await db.SaveChangesAsync(); });
        }
    }

    [Fact]
    public async Task ARefundRequest_NamesTheBuyerAndTheShow()
    {
        var id = await InDbAsync(async db =>
        {
            var payment = new Payment
            {
                OrderId = $"NM-{Guid.NewGuid():N}"[..30], TransactionId = $"NM{Guid.NewGuid():N}"[..16],
                GrossAmount = 100_000m, PlatformFee = 5_000m, TaxWithheld = 5_000m, NetAmount = 90_000m,
                Status = PaymentStatus.Confirmed, ReferenceType = "TicketHold", ReferenceId = "0", CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(payment); await db.SaveChangesAsync();
            db.Add(new Ticket
            {
                Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId, PriceId = SeedHelper.TicketPriceId, TierId = SeedHelper.TicketTierId,
                ShowId = SeedHelper.ShowId, PaymentId = payment.Id, Status = TicketStatus.Cancelled,
                PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow
            });
            var refund = new RefundRequest
            {
                PaymentId = payment.Id, RequestedBy = SeedHelper.AudienceId, Reason = "ten", AmountRequested = 100_000m,
                RefundPercentage = 100m, Status = RefundRequestStatus.Pending
            };
            db.Add(refund); await db.SaveChangesAsync(); return refund.Id;
        });
        var buyer = await InDbAsync(db => db.Users.Where(u => u.Id == SeedHelper.AudienceId).Select(u => u.FullName).SingleAsync());

        var row = await ItemAsync(await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetAsync("/api/v1/admin/refund-requests?pageSize=200"), id);

        row.GetProperty("requesterName").GetString().Should().Be(buyer);
        row.GetProperty("showName").GetString().Should().Be(await ShowNameAsync());
        row.GetProperty("ticketCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task AnFnbOrder_NamesTheCustomer_ForTheVenueOnly()
    {
        var id = await InDbAsync(async db =>
        {
            var o = new FnbOrder
            {
                LoungeId = SeedHelper.LoungeId, AudienceUserId = SeedHelper.AudienceId, TableNote = "Bàn A3",
                Status = FnbOrderStatus.Pending, PaymentMethod = PaymentMethod.Cash, TotalAmount = 50_000m
            };
            db.Add(o); await db.SaveChangesAsync(); return o.Id;
        });
        var customer = await InDbAsync(db => db.Users.Where(u => u.Id == SeedHelper.AudienceId).Select(u => u.FullName).SingleAsync());

        var forVenue = await ItemAsync(await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner")
            .GetAsync($"/api/v1/fnb-orders?loungeId={SeedHelper.LoungeId}&pageSize=200"), id);
        forVenue.GetProperty("customerName").GetString().Should().Be(customer,
            "nhân viên gọi khách bằng tên, không đọc mã đơn #00000008");

        var forBuyer = await ItemAsync(await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
            .GetAsync("/api/v1/fnb-orders/my?pageSize=200"), id);
        forBuyer.GetProperty("customerName").ValueKind.Should().Be(JsonValueKind.Null, "khách xem đơn của chính mình không cần");
    }
}

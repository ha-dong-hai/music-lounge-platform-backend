using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF3;

/// <summary>
/// Chi tiết vé phải biết vé đó thuộc buổi hòa nhạc NÀO, không chỉ biết tên buổi diễn.
///
/// <para><c>TicketListItemDto</c> có <c>ShowId</c> ngay từ đầu, còn <c>TicketDetailDto</c> thì không —
/// cùng một thực thể mà màn hình danh sách biết nhiều hơn màn hình chi tiết. Hậu quả ở giao diện: từ
/// trang chi tiết vé không dẫn sang trang buổi hòa nhạc hay trang xem trực tuyến được, vì chỉ có TÊN
/// buổi diễn chứ không có mã. Tên thì không tra ra đường dẫn.</para>
///
/// <para>Phiên làm giao diện phát hiện khi soát theo LUỒNG nghiệp vụ (người mua vé xong thì đi đâu
/// tiếp) thay vì theo độ phủ giao diện lập trình — độ phủ sẽ báo xanh hết, vì mọi endpoint đều tồn tại
/// và chạy đúng, chỉ là không ai đi tới được. (MLACP-478)</para>
/// </summary>
[Collection("Integration")]
public sealed class TicketDetailKnowsItsShowTests
{
    private readonly ApiFactory _factory;

    public TicketDetailKnowsItsShowTests(ApiFactory factory) => _factory = factory;

    private static JsonElement Data(string body)
    {
        var root = JsonDocument.Parse(body).RootElement;
        return root.TryGetProperty("data", out var d) ? d : root;
    }

    /// <summary>
    /// Dựng vé RIÊNG chứ không mượn vé seed dùng chung. Bản đầu của phép kiểm này dùng
    /// <c>SeedHelper.AudienceTicketId</c>: chạy một mình thì xanh, chạy cả bộ thì đỏ — vé đó bị phép kiểm
    /// khác chuyển nhượng/đổi trạng thái nên không còn nằm trong danh sách vé của khán giả nữa. Phụ thuộc
    /// vào trạng thái seed dùng chung là phụ thuộc vào THỨ TỰ CHẠY.
    /// </summary>
    private async Task<Guid> TaoVeRiengAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = Guid.NewGuid();
        db.Add(new Ticket
        {
            Id = id,
            BuyerId = SeedHelper.AudienceId,
            PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId,
            ShowId = SeedHelper.ShowId,
            Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task ChiTietVe_TraVeShowId_KhopVoiDanhSachVe()
    {
        var veId = await TaoVeRiengAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var chiTiet = await client.GetAsync($"/api/v1/tickets/{veId}");
        chiTiet.StatusCode.Should().Be(HttpStatusCode.OK);

        var d = Data(await chiTiet.Content.ReadAsStringAsync());
        d.TryGetProperty("showId", out var showId).Should().BeTrue(
            "không có mã buổi diễn thì từ trang chi tiết vé không dẫn đi đâu được");
        showId.GetInt32().Should().Be(SeedHelper.ShowId);

        // Hai đường đọc cùng một thực thể thì phải nói cùng một điều.
        var ds = await client.GetAsync("/api/v1/tickets/my?page=1&pageSize=50");
        ds.StatusCode.Should().Be(HttpStatusCode.OK);

        var dong = Data(await ds.Content.ReadAsStringAsync())
            .GetProperty("items").EnumerateArray()
            .FirstOrDefault(t => t.GetProperty("id").GetString() == veId.ToString());

        dong.ValueKind.Should().NotBe(JsonValueKind.Undefined, "phải tìm được đúng vé đó trong danh sách");
        dong.GetProperty("showId").GetInt32().Should().Be(showId.GetInt32(),
            "danh sách và chi tiết mà lệch nhau thì một trong hai đang nói sai");
    }
}

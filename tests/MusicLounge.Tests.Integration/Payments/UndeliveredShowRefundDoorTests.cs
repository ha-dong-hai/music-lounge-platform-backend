using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-338. Người mua vé một buổi diễn <b>chưa từng được tổ chức</b> bấm huỷ vé để đòi lại tiền,
/// và hệ thống trả về <i>"Không thể hủy vé sau khi event đã kết thúc."</i> — một câu khẳng định buổi
/// diễn đã diễn ra xong, trong khi nó có thể chưa bao giờ bắt đầu.
///
/// <para>Ba chốt trong <c>CancelTicketCommandHandler</c> đều chặn đúng tình huống này:
/// <c>!CancellationAllowed</c>, <c>Status == Ended || neverStartedButOverdue</c>, và
/// <c>CancellationDeadlineHours</c>. Và nếu qua được thì tỉ lệ hoàn lấy từ <b>chính sách huỷ vé của
/// phòng trà</b>.</para>
///
/// <para><b>Đây không phải huỷ vé — đây là không giao được hàng.</b> Điều 36 Luật BVQLNTD 2023 xếp
/// việc cung cấp dịch vụ không đúng nội dung đã công bố vào nhóm phải khắc phục, trong đó có chấm
/// dứt và hoàn tiền. Eventbrite cũng tách rõ: sự kiện không diễn ra thì phải hoàn <b>bất kể</b>
/// chính sách của ban tổ chức. Và chính codebase này đã theo nguyên tắc đó ở chỗ khác — mọi đường
/// hoàn tiền do nền tảng ép đều hardcode 100%.</para>
/// </summary>
[Collection("Integration")]
public sealed class UndeliveredShowRefundDoorTests
{
    private readonly ApiFactory _factory;

    public UndeliveredShowRefundDoorTests(ApiFactory factory) => _factory = factory;

    /// <param name="actualStart">
    /// <c>null</c> tái hiện buổi diễn chưa từng được bấm Bắt đầu.
    /// </param>
    private async Task<Guid> TicketOnPastShowAsync(
        LoungeShowStatus status,
        DateTimeOffset? actualStart,
        bool cancellationAllowed = true,
        int? cancellationDeadlineHours = null,
        decimal? refundPercentage = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var scheduledStart = DateTimeOffset.UtcNow.AddDays(-3);

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"Show {Guid.NewGuid():N}"[..18],
            Status = status,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledStart.AddHours(2),
            ActualStart = actualStart,
            ActualEnd = status == LoungeShowStatus.Ended ? scheduledStart.AddHours(2) : null,
            CancellationAllowed = cancellationAllowed,
            CancellationDeadlineHours = cancellationDeadlineHours,
            RefundPercentage = refundPercentage,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = $"MLACP338-{Guid.NewGuid():N}"[..30],
            PayerId = SeedHelper.AudienceId,
            GrossAmount = 500_000m,
            NetAmount = 500_000m,
            Status = PaymentStatus.Confirmed,
            ReferenceType = "TicketHold",
            ReferenceId = "0",
            TransactionId = $"D{Guid.NewGuid():N}"[..16],
            PaidAt = DateTimeOffset.UtcNow.AddDays(-4),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-4)
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId,
            ShowId = show.Id,
            PaymentId = payment.Id,
            Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-4)
        };
        db.Add(ticket);
        await db.SaveChangesAsync();

        return ticket.Id;
    }

    private Task<HttpResponseMessage> CancelAsync(Guid ticketId)
        => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
            .PostAsync($"/api/v1/tickets/{ticketId}/cancel", null);

    private async Task<RefundRequest?> RefundForAsync(Guid ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var paymentId = await db.Tickets.Where(t => t.Id == ticketId)
            .Select(t => t.PaymentId).FirstAsync();
        return await db.RefundRequests.FirstOrDefaultAsync(r => r.PaymentId == paymentId);
    }

    // ── Cửa phải mở ─────────────────────────────────────────────────────────

    [Fact]
    public async Task BuoiDienTuDongDongMaChuaTungBatDauThiVanDoiTienDuoc()
    {
        var ticketId = await TicketOnPastShowAsync(LoungeShowStatus.Ended, actualStart: null);

        var res = await CancelAsync(ticketId);

        res.StatusCode.Should().Be(HttpStatusCode.OK,
            "câu \"không thể hủy vé sau khi event đã kết thúc\" khẳng định buổi diễn đã diễn ra xong " +
            "— với buổi diễn chưa từng bắt đầu thì đó là nói sai sự thật với người đã trả tiền");

        (await RefundForAsync(ticketId))!.RefundPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task BuoiDienKetOPublishedQuaGioCungDoiTienDuoc()
    {
        // Khong bat nguoi mua phai cho job tu dong chay dung thi moi doi duoc tien — codebase nay
        // da nam lan co job chet lang le vi quen dang ky DI.
        var ticketId = await TicketOnPastShowAsync(LoungeShowStatus.Published, actualStart: null);

        var res = await CancelAsync(ticketId);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await RefundForAsync(ticketId))!.RefundPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task ChinhSachCamHuyVeCuaPhongTraKhongApDuocKhiChinhHoKhongToChuc()
    {
        var ticketId = await TicketOnPastShowAsync(
            LoungeShowStatus.Ended, actualStart: null, cancellationAllowed: false);

        var res = await CancelAsync(ticketId);

        res.StatusCode.Should().Be(HttpStatusCode.OK,
            "không giao được hàng thì chính sách huỷ của bên bán không còn là chuyện đang bàn");
    }

    [Fact]
    public async Task HanHuyVeVaTiLeCuaPhongTraDeuKhongApDuoc()
    {
        // Hạn huỷ 48h trước giờ diễn đã trôi qua từ lâu, và phòng trà chỉ hoàn 10%.
        var ticketId = await TicketOnPastShowAsync(
            LoungeShowStatus.Ended, actualStart: null,
            cancellationDeadlineHours: 48, refundPercentage: 10m);

        var res = await CancelAsync(ticketId);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await RefundForAsync(ticketId))!.RefundPercentage
            .Should().Be(100m, "10% là chính sách cho việc khách đổi ý, không phải cho việc phòng trà không diễn");
    }

    [Fact]
    public async Task LyDoGhiTrenYeuCauPhaiNoiDungChuyenDaXayRa()
    {
        var ticketId = await TicketOnPastShowAsync(LoungeShowStatus.Ended, actualStart: null);

        await CancelAsync(ticketId);

        (await RefundForAsync(ticketId))!.Reason
            .Should().Contain("không được tổ chức",
                "hồ sơ phải phân biệt được khách đổi ý với phòng trà không giao hàng");
    }

    // ── Cửa vẫn phải đóng khi buổi diễn có thật ─────────────────────────────

    [Fact]
    public async Task BuoiDienDaDienRaThatThiVanChanNhuCu()
    {
        // Chiều đối chứng, và là chiều dễ làm hỏng nhất: mở cửa cho trường hợp không giao được
        // không được biến thành mở cửa cho mọi người đòi tiền sau khi đã xem xong.
        var start = DateTimeOffset.UtcNow.AddDays(-3);
        var ticketId = await TicketOnPastShowAsync(LoungeShowStatus.Ended, actualStart: start);

        var res = await CancelAsync(ticketId);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "dịch vụ đã được cung cấp xong — chốt cũ đúng với trường hợp này");
        (await RefundForAsync(ticketId)).Should().BeNull();
    }
}

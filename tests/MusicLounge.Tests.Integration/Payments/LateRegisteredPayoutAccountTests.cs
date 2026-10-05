using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-640. Khoản quyết toán tạo lúc phòng trà CHƯA có tài khoản nhận tiền (BankAccountId = null) phải được trả khi
/// phòng trà thêm tài khoản và tài khoản được xác minh — trước đây nó kẹt mãi: không chỗ nào gán lại tài khoản, job chỉ
/// ghi log rồi bỏ qua mỗi lần chạy (đo trên DB cục bộ 05/10/2026: 37 khoản / 8.604.000đ). Mỗi bài một chủ phòng trà riêng.
/// </summary>
[Collection("Integration")]
public sealed class LateRegisteredPayoutAccountTests
{
    private readonly ApiFactory _factory;

    public LateRegisteredPayoutAccountTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(Guid OwnerId, Guid LoungeId, Guid SettlementId);
    private sealed record ReviewBody(string Decision, string Note);

    private ApplicationDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    /// <summary>Chủ phòng trà đã duyệt CCCD, phòng trà KHÔNG có tài khoản nào, và một khoản quyết toán đã tới hạn của buổi
    /// đã diễn trọn — tạo với tài khoản null, đúng như ScheduleSettlementHandler ghi khi phòng trà chưa có tài khoản.</summary>
    private async Task<Venue> SettlementWithoutAccountAsync(SettlementStatus status = SettlementStatus.Scheduled)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var owner = new User
        {
            Email = $"payee640-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà 640", Role = UserRole.Owner,
            CitizenCardSubmittedAt = DateTimeOffset.UtcNow.AddDays(-2), CitizenCardReviewStatus = KycReviewStatus.Approved,
            CitizenCardVerifiedName = "Chủ phòng trà 640"
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue640-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        var start = DateTimeOffset.UtcNow.AddDays(-20);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"Show {Guid.NewGuid():N}"[..18], Status = LoungeShowStatus.Ended,
            ScheduledStart = start, ScheduledEnd = start.AddHours(2), ActualStart = start, ActualEnd = start.AddHours(2),
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();
        var payment = new Payment
        {
            OrderId = $"MLACP640-{Guid.NewGuid():N}"[..30], PayerId = SeedHelper.AudienceId, GrossAmount = 1_000_000m,
            NetAmount = 880_000m, Status = PaymentStatus.Confirmed, ReferenceType = "TicketHold", ReferenceId = "0",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(payment);
        await db.SaveChangesAsync();
        db.Add(new Ticket
        {
            Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId, PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId, ShowId = show.Id, PaymentId = payment.Id, Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow
        });
        var settlement = new Settlement
        {
            OwnerId = owner.Id, PaymentId = payment.Id, BankAccountId = null,
            ReleaseType = SettlementReleaseType.Partial70, GrossAmount = 1_000_000m, PreRateApplied = 0.70m,
            PostRateApplied = 0.30m, NetAmount = 616_000m, Status = status,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(settlement);
        await db.SaveChangesAsync();
        return new Venue(owner.Id, lounge.Id, settlement.Id);
    }

    /// <summary>Phòng trà thêm tài khoản mặc định SAU khi khoản quyết toán đã được tạo.</summary>
    private async Task<Guid> AddDefaultAccountAsync(Guid loungeId, bool verified)
    {
        using var scope = _factory.Services.CreateScope();
        var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
        var account = new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = loungeId, BankName = "Test Bank",
            AccountNumber = pii.Encrypt("0000000640"), AccountHolder = "Chu phong tra 640", IsDefault = true, IsVerified = verified
        };
        Db(scope).Add(account);
        await Db(scope).SaveChangesAsync();
        return account.Id;
    }

    private async Task RunReleaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>().ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<Settlement> ReloadAsync(Guid settlementId)
    {
        using var scope = _factory.Services.CreateScope();
        return await Db(scope).Settlements.AsNoTracking().SingleAsync(s => s.Id == settlementId);
    }

    [Fact]
    public async Task AnAccountAddedAndVerifiedLater_IsWhereTheHeldPayoutGoes()
    {
        var venue = await SettlementWithoutAccountAsync();
        await RunReleaseAsync();
        (await ReloadAsync(venue.SettlementId)).Status.Should().Be(SettlementStatus.Scheduled, "test premise: no account yet");

        var accountId = await AddDefaultAccountAsync(venue.LoungeId, verified: true);
        await RunReleaseAsync();

        var after = await ReloadAsync(venue.SettlementId);
        after.Status.Should().Be(SettlementStatus.Released,
            "the venue now has a verified default account — before MLACP-640 this tranche stayed deferred forever");
        after.BankAccountId.Should().Be(accountId, "the payout must record where the money went");
    }

    [Fact]
    public async Task AnAccountAddedButNotYetVerified_StillHoldsThePayout()
    {
        var venue = await SettlementWithoutAccountAsync();
        var accountId = await AddDefaultAccountAsync(venue.LoungeId, verified: false);

        await RunReleaseAsync();

        var after = await ReloadAsync(venue.SettlementId);
        after.Status.Should().Be(SettlementStatus.Scheduled, "an unverified account is still not paid into (MLACP-395)");
        after.BankAccountId.Should().Be(accountId);
    }

    [Fact]
    public async Task WithNoAccountAtAll_TheOwnerIsToldWhyTheMoneyIsHeld()
    {
        var venue = await SettlementWithoutAccountAsync();

        await RunReleaseAsync();

        (await ReloadAsync(venue.SettlementId)).Status.Should().Be(SettlementStatus.Scheduled);
        using var scope = _factory.Services.CreateScope();
        (await Db(scope).Notifications.AsNoTracking()
                .Where(n => n.UserId == venue.OwnerId && n.Type == NotificationType.PayoutOnHold)
                .ToListAsync())
            .Should().ContainSingle("before MLACP-640 the reason lived only in a log line")
            .Which.Body.Should().Contain("chưa có tài khoản nhận tiền");
    }

    [Fact]
    public async Task AnAdminReleasingAParkedTranche_UsesTheAccountAddedLater()
    {
        var venue = await SettlementWithoutAccountAsync(SettlementStatus.PendingReview);
        var accountId = await AddDefaultAccountAsync(venue.LoungeId, verified: true);

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .PostAsJsonAsync($"/api/v1/admin/settlements/{venue.SettlementId}/review", new ReviewBody("Release", "Da doi soat"));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent, await res.Content.ReadAsStringAsync());
        var after = await ReloadAsync(venue.SettlementId);
        after.Status.Should().Be(SettlementStatus.Released);
        after.BankAccountId.Should().Be(accountId);
    }
}

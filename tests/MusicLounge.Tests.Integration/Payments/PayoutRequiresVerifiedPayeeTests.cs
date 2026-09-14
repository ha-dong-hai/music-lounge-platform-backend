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
/// MLACP-395. Nền tảng chỉ chuyển tiền quyết toán cho chủ phòng trà đã được duyệt CCCD/CMND, vào tài khoản đã được Admin
/// xác minh. Chưa đạt thì khoản tiền được HOÃN (vẫn Scheduled) và người gỡ được chặn được báo: chủ phòng trà khi họ cần nộp
/// hoặc nộp lại hồ sơ, Admin khi hồ sơ chờ duyệt hoặc tài khoản chờ xác minh. Mỗi bài một chủ phòng trà riêng.
/// </summary>
[Collection("Integration")]
public sealed class PayoutRequiresVerifiedPayeeTests
{
    private readonly ApiFactory _factory;

    public PayoutRequiresVerifiedPayeeTests(ApiFactory factory) => _factory = factory;

    private sealed record Payee(int OwnerId, int BankAccountId, int SettlementId);

    private ApplicationDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    /// <summary>Chủ phòng trà với trạng thái CCCD cho trước, tài khoản nhận tiền (đã/chưa xác minh), và một khoản quyết
    /// toán đã tới hạn của một buổi diễn đã diễn trọn — thứ duy nhất có thể còn chặn là việc xác minh người nhận.</summary>
    private async Task<Payee> DuePayoutAsync(KycReviewStatus? identity, bool accountVerified)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var owner = new User
        {
            Email = $"payee395-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà 395", Role = UserRole.Owner,
            CitizenCardSubmittedAt = identity is null ? null : DateTimeOffset.UtcNow.AddDays(-2),
            CitizenCardReviewStatus = identity
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue395-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
        var account = new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id, BankName = "Test Bank",
            AccountNumber = pii.Encrypt("0000000395"), AccountHolder = "Chu phong tra 395",
            IsDefault = true, IsVerified = accountVerified
        };
        db.Add(account);
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
            OrderId = $"MLACP395-{Guid.NewGuid():N}"[..30], PayerId = SeedHelper.AudienceId, GrossAmount = 1_000_000m,
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
            OwnerId = owner.Id, PaymentId = payment.Id, BankAccountId = account.Id,
            ReleaseType = SettlementReleaseType.Partial70, GrossAmount = 1_000_000m, PreRateApplied = 0.70m,
            PostRateApplied = 0.30m, NetAmount = 616_000m, Status = SettlementStatus.Scheduled,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(settlement);
        await db.SaveChangesAsync();
        return new Payee(owner.Id, account.Id, settlement.Id);
    }

    private async Task RunReleaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>().ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<SettlementStatus> StatusAsync(int settlementId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await Db(scope).Settlements.AsNoTracking().SingleAsync(s => s.Id == settlementId)).Status;
    }

    /// <summary>Thông báo "đang giữ tiền" gửi cho <paramref name="recipientId"/> về chủ phòng trà <paramref name="ownerId"/>.</summary>
    private async Task<List<Notification>> HeldNoticesAsync(int recipientId, int ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        return await Db(scope).Notifications.AsNoTracking()
            .Where(n => n.UserId == recipientId && n.Type == NotificationType.PayoutOnHold && n.ReferenceId == ownerId.ToString())
            .ToListAsync();
    }

    private Task<HttpResponseMessage> ReviewAccountAsync(int bankAccountId, bool approve, string? note = null)
        => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .PostAsJsonAsync($"/api/v1/admin/bank-accounts/{bankAccountId}/review", new { Approve = approve, Note = note });

    // ── Giải ngân ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnOwnerWhoNeverVerifiedTheirIdentity_IsNotPaid_AndIsToldWhatToDo()
    {
        var payee = await DuePayoutAsync(identity: null, accountVerified: true);

        await RunReleaseAsync();

        (await StatusAsync(payee.SettlementId)).Should().Be(SettlementStatus.Scheduled,
            "held, not cancelled — it is paid on a later run once the payee is verified");
        (await HeldNoticesAsync(payee.OwnerId, payee.OwnerId)).Should().ContainSingle()
            .Which.Body.Should().Contain("nộp CCCD/CMND");
        (await HeldNoticesAsync(SeedHelper.AdminId, payee.OwnerId)).Should().BeEmpty("there is nothing for an Admin to review yet");
    }

    [Fact]
    public async Task AnOwnerWhoseIdentityWasRejected_IsNotPaid_AndIsAskedToResubmit()
    {
        var payee = await DuePayoutAsync(KycReviewStatus.Rejected, accountVerified: true);

        await RunReleaseAsync();

        (await StatusAsync(payee.SettlementId)).Should().Be(SettlementStatus.Scheduled);
        (await HeldNoticesAsync(payee.OwnerId, payee.OwnerId)).Should().ContainSingle()
            .Which.Body.Should().Contain("nộp lại");
    }

    [Fact]
    public async Task AnIdentityAwaitingReview_HoldsThePayout_AndTellsTheAdmins()
    {
        var payee = await DuePayoutAsync(KycReviewStatus.Pending, accountVerified: true);

        await RunReleaseAsync();

        (await StatusAsync(payee.SettlementId)).Should().Be(SettlementStatus.Scheduled);
        (await HeldNoticesAsync(SeedHelper.AdminId, payee.OwnerId)).Should().ContainSingle()
            .Which.Body.Should().Contain("chờ duyệt", "otherwise the money waits on a review nobody knows is needed");
        (await HeldNoticesAsync(payee.OwnerId, payee.OwnerId)).Should().BeEmpty();
    }

    [Fact]
    public async Task AnUnverifiedPayoutAccount_HoldsThePayout_AndTellsTheAdmins()
    {
        var payee = await DuePayoutAsync(KycReviewStatus.Approved, accountVerified: false);

        await RunReleaseAsync();

        (await StatusAsync(payee.SettlementId)).Should().Be(SettlementStatus.Scheduled);
        (await HeldNoticesAsync(SeedHelper.AdminId, payee.OwnerId)).Should().ContainSingle()
            .Which.Body.Should().Contain("tài khoản nhận tiền chưa được xác minh");
    }

    [Fact]
    public async Task AVerifiedOwnerAndAccount_ArePaidAsBefore()
    {
        var payee = await DuePayoutAsync(KycReviewStatus.Approved, accountVerified: true);

        await RunReleaseAsync();

        (await StatusAsync(payee.SettlementId)).Should().Be(SettlementStatus.Released);
        (await HeldNoticesAsync(payee.OwnerId, payee.OwnerId)).Should().BeEmpty();
    }

    [Fact]
    public async Task RunningTheJobAgain_DoesNotRepeatTheNotice()
    {
        var payee = await DuePayoutAsync(identity: null, accountVerified: true);

        await RunReleaseAsync();
        await RunReleaseAsync();

        (await HeldNoticesAsync(payee.OwnerId, payee.OwnerId)).Should().HaveCount(1, "the job runs daily — one notice a week is enough");
    }

    [Fact]
    public async Task EditingAVerifiedAccount_HoldsTheNextPayoutAgain()
    {
        var payee = await DuePayoutAsync(KycReviewStatus.Approved, accountVerified: true);

        (await _factory.CreateAuthenticatedClient(payee.OwnerId, "Owner").PutAsJsonAsync(
                $"/api/v1/bank-accounts/{payee.BankAccountId}",
                new { BankName = "Other Bank", AccountNumber = "0000000999", AccountHolder = "Chu phong tra 395", IsDefault = true }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        await RunReleaseAsync();

        (await StatusAsync(payee.SettlementId)).Should().Be(SettlementStatus.Scheduled,
            "a changed account number must be verified again before money goes to it");
    }

    // ── Admin xác minh tài khoản nhận tiền ───────────────────────────────────

    [Fact]
    public async Task OnceTheAccountIsVerified_TheHeldPayoutIsReleasedOnTheNextRun()
    {
        var payee = await DuePayoutAsync(KycReviewStatus.Approved, accountVerified: false);
        await RunReleaseAsync();
        (await StatusAsync(payee.SettlementId)).Should().Be(SettlementStatus.Scheduled);

        (await ReviewAccountAsync(payee.BankAccountId, approve: true)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await RunReleaseAsync();

        (await StatusAsync(payee.SettlementId)).Should().Be(SettlementStatus.Released);
        using var scope = _factory.Services.CreateScope();
        (await Db(scope).Notifications.AsNoTracking().AnyAsync(n =>
                n.UserId == payee.OwnerId && n.Type == NotificationType.KycReviewResult
                && n.ReferenceId == payee.BankAccountId.ToString()))
            .Should().BeTrue("the owner is told their account was verified");
    }

    [Fact]
    public async Task AnAccountCannotBeVerifiedBeforeTheOwnersIdentity()
    {
        var payee = await DuePayoutAsync(KycReviewStatus.Pending, accountVerified: false);

        (await ReviewAccountAsync(payee.BankAccountId, approve: true)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using var scope = _factory.Services.CreateScope();
        (await Db(scope).Set<BankAccount>().AsNoTracking().SingleAsync(a => a.Id == payee.BankAccountId))
            .IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task RejectingAnAccount_RequiresAReason()
    {
        var payee = await DuePayoutAsync(KycReviewStatus.Approved, accountVerified: false);

        (await ReviewAccountAsync(payee.BankAccountId, approve: false, note: null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task APerformersAccount_IsNotReviewedHere()
    {
        int accountId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
            var account = new BankAccount
            {
                OwnerType = BankAccountOwnerType.Performer, OwnerId = SeedHelper.PerformerId, BankName = "Test Bank",
                AccountNumber = pii.Encrypt("0000000396"), AccountHolder = "Nghe si", IsDefault = false, IsVerified = false
            };
            db.Add(account);
            await db.SaveChangesAsync();
            accountId = account.Id;
        }

        (await ReviewAccountAsync(accountId, approve: true)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "a performer confirms their own account through the emailed link");
    }
}

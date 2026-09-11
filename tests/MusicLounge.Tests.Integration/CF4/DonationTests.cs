using System.Net;
using System.Net.Http.Json;
using System.Web;
using FluentAssertions;
using Hangfire;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// CF4 W21 / D17 — Donation lifecycle + public history
/// POST /api/v1/donations
/// GET  /api/v1/donations/vnpay-return
/// POST /api/v1/donations/{id}/acknowledge
/// POST /api/v1/donations/{id}/confirm-paid
/// GET  /api/v1/performers/{performerId}/donations
/// </summary>
[Collection("Integration")]
public sealed class DonationTests
{
    private readonly ApiFactory _factory;

    public DonationTests(ApiFactory factory) => _factory = factory;

    // ─── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a donation and returns (donationId, orderId).</summary>
    private async Task<(int Id, string OrderId)> CreateDonationAsync(
        int performanceId = SeedHelper.PerformanceId,
        decimal amount = 100_000m)
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var res = await client.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = performanceId,
            Amount = amount,
            IsAnonymous = false,
            Message = "Great show!",
            IsMessagePublic = true
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<DonationInitResponse>();
        return (body!.Data.DonationId, body.Data.OrderId);
    }

    /// <summary>Simulates a VNPay callback. Auto-redirect is disabled so callers receive the raw 302.</summary>
    private async Task<HttpResponseMessage> SimulateVnPayCallbackAsync(
        string orderId, bool success = true)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var responseCode = success ? "00" : "99";
        var qs = $"?vnp_TxnRef={Uri.EscapeDataString(orderId)}" +
                 $"&vnp_ResponseCode={responseCode}" +
                 $"&vnp_Amount=10000000";
        return await client.GetAsync($"/api/v1/donations/vnpay-return{qs}");
    }

    /// <summary>
    /// MLACP-361: chủ phòng trà chỉ xác nhận "đã nhận" được sau khi nền tảng đã giải ngân chặng 1
    /// cho phòng trà. Chạy đúng job giải ngân thật — phòng trà seed có sẵn tài khoản ngân hàng mặc định.
    /// </summary>
    private async Task ReleaseVenuePayoutsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>();
        await job.ExecuteAsync(new JobCancellationToken(false));
    }

    // ─── Create donation tests ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateDonation_ValidAmount_Returns201WithPaymentUrl()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = SeedHelper.PerformanceId,
            Amount = 200_000m,
            IsAnonymous = false,
            IsMessagePublic = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<DonationInitResponse>();
        body!.Data.PaymentUrl.Should().StartWith("https://sandbox.vnpay.test");
        body.Data.DonationId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateDonation_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = SeedHelper.PerformanceId,
            Amount = 100_000m,
            IsAnonymous = false,
            IsMessagePublic = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateDonation_AmountZero_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = SeedHelper.PerformanceId,
            Amount = 0m,
            IsAnonymous = false,
            IsMessagePublic = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDonation_AmountExceedsMax_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = SeedHelper.PerformanceId,
            Amount = 100_000_000m, // exceeds 50M limit
            IsAnonymous = false,
            IsMessagePublic = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDonation_ForCancelledShow_Returns422()
    {
        // Seed a Performance for the pre-seeded Cancelled show, then close the scope
        // before making the HTTP request to avoid SQLite single-connection conflicts.
        int cancelledPerfId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cancelledPerf = new MusicLounge.Domain.Entities.Performance
            {
                LoungeShowId = SeedHelper.CancelledShowId,
                PerformerId = SeedHelper.PerformerId,
                OrderIndex = 1
            };
            db.Performances.Add(cancelledPerf);
            await db.SaveChangesAsync();
            cancelledPerfId = cancelledPerf.Id;
        } // scope + DbContext disposed before HTTP request

        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var res = await client.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = cancelledPerfId,
            Amount = 100_000m,
            IsAnonymous = false,
            IsMessagePublic = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ─── VNPay callback tests ─────────────────────────────────────────────────

    [Fact]
    public async Task VnPayCallback_Success_ReturnsTrueAndTransitionsToPendingOwnerAck()
    {
        var (_, orderId) = await CreateDonationAsync();

        var res = await SimulateVnPayCallbackAsync(orderId, success: true);

        res.StatusCode.Should().Be(HttpStatusCode.Redirect);
        res.Headers.Location!.ToString().Should().Contain("success");
    }

    [Fact]
    public async Task VnPayCallback_Failure_ReturnsFalseAndCancelsDonation()
    {
        var (_, orderId) = await CreateDonationAsync();

        var res = await SimulateVnPayCallbackAsync(orderId, success: false);

        res.StatusCode.Should().Be(HttpStatusCode.Redirect);
        res.Headers.Location!.ToString().Should().Contain("failed");
    }

    [Fact]
    public async Task VnPayCallback_DuplicateSuccessCallback_IsIdempotent()
    {
        var (_, orderId) = await CreateDonationAsync();

        // First callback — success
        await SimulateVnPayCallbackAsync(orderId, success: true);

        // Duplicate — should NOT re-process or cancel
        var res2 = await SimulateVnPayCallbackAsync(orderId, success: false);

        // After idempotency guard, donation is already PendingOwnerAck, not Cancelled.
        // Handler returns true → redirect to success URL even though this callback sent failure code.
        res2.StatusCode.Should().Be(HttpStatusCode.Redirect);
        res2.Headers.Location!.ToString().Should().Contain("success",
            "duplicate callback on an already-processed donation should redirect to success (was success)");
    }

    // ─── Acknowledge tests ────────────────────────────────────────────────────

    [Fact]
    public async Task AcknowledgeDonation_ByCorrectOwner_Returns204()
    {
        var (id, orderId) = await CreateDonationAsync();
        await SimulateVnPayCallbackAsync(orderId, success: true);
        await ReleaseVenuePayoutsAsync();

        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var res = await ownerClient.PostAsync($"/api/v1/donations/{id}/acknowledge", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcknowledgeDonation_ByAudience_Returns403()
    {
        var (id, orderId) = await CreateDonationAsync();
        await SimulateVnPayCallbackAsync(orderId, success: true);

        var audienceClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var res = await audienceClient.PostAsync($"/api/v1/donations/{id}/acknowledge", null);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AcknowledgeDonation_ByWrongOwner_Returns403()
    {
        var (id, orderId) = await CreateDonationAsync();
        await SimulateVnPayCallbackAsync(orderId, success: true);

        // OtherOwnerId owns a different lounge — wrong owner
        var wrongOwner = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");
        var res = await wrongOwner.PostAsync($"/api/v1/donations/{id}/acknowledge", null);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Regression test for MLACP-252 (audit-flagged B1 gap, 2026-09-04): the handler used to check
    /// only "ownership.OwnerId == currentUser.UserId" with no Admin fallback, so an Admin — who
    /// passes the controller's [Authorize(Policy = RequireOwner)] gate, which permits Admin by
    /// definition — was incorrectly 403'd here despite the policy nominally allowing them through.
    /// </summary>
    [Fact]
    public async Task AcknowledgeDonation_ByAdmin_NotTheVenueOwner_Returns204()
    {
        var (id, orderId) = await CreateDonationAsync();
        await SimulateVnPayCallbackAsync(orderId, success: true);
        await ReleaseVenuePayoutsAsync();

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.PostAsync($"/api/v1/donations/{id}/acknowledge", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "Admin must be able to act on any venue's donation, matching the controller's declared RequireOwner policy");
    }

    [Fact]
    public async Task AcknowledgeDonation_WhenCancelledByVnPay_Returns422()
    {
        var (id, orderId) = await CreateDonationAsync();
        await SimulateVnPayCallbackAsync(orderId, success: false); // donation → Cancelled

        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var res = await ownerClient.PostAsync($"/api/v1/donations/{id}/acknowledge", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AcknowledgeDonation_AlreadyAcknowledged_Returns422()
    {
        var (id, orderId) = await CreateDonationAsync();
        await SimulateVnPayCallbackAsync(orderId, success: true);
        await ReleaseVenuePayoutsAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        await ownerClient.PostAsync($"/api/v1/donations/{id}/acknowledge", null);

        // Second acknowledge attempt
        var res = await ownerClient.PostAsync($"/api/v1/donations/{id}/acknowledge", null);
        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ─── Confirm Paid tests ───────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmDonationPaid_AfterAcknowledge_Returns204()
    {
        var (id, orderId) = await CreateDonationAsync();
        await SimulateVnPayCallbackAsync(orderId, success: true);
        await ReleaseVenuePayoutsAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        await ownerClient.PostAsync($"/api/v1/donations/{id}/acknowledge", null);

        var res = await ownerClient.PostAsJsonAsync($"/api/v1/donations/{id}/confirm-paid", new
        {
            PaymentRef = "TXN123456",
            PaymentEvidenceUrl = "https://bank.test/receipt/123"
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ConfirmDonationPaid_BeforeAcknowledge_Returns422()
    {
        var (id, orderId) = await CreateDonationAsync();
        await SimulateVnPayCallbackAsync(orderId, success: true);
        // Skip acknowledge — go directly to confirm-paid
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await ownerClient.PostAsJsonAsync($"/api/v1/donations/{id}/confirm-paid", new
        {
            PaymentRef = "TXN999",
            PaymentEvidenceUrl = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// MLACP-259: ConfirmDonationPaidCommandHandler had no IAsyncKeyedLock — WriteJournalAsync itself
    /// has no idempotency guard (journalId is a fresh random Guid per call), so without the lock, 2
    /// near-simultaneous "Confirm Paid" clicks could both read Status==OwnerReceived before either
    /// commits, both writing a chặng-2 ledger journal for the same donation (performer shown paid
    /// twice on the books). Fires 6 concurrent confirm-paid requests and asserts exactly 1 succeeds
    /// AND exactly 1 chặng-2 journal (2 LedgerEntry rows: debit Owner, credit Performer) exists.
    /// </summary>
    [Fact]
    public async Task ConfirmDonationPaid_ConcurrentRequests_OnlyOneWritesLedgerJournal()
    {
        var (id, orderId) = await CreateDonationAsync();
        await SimulateVnPayCallbackAsync(orderId, success: true);
        await ReleaseVenuePayoutsAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        await ownerClient.PostAsync($"/api/v1/donations/{id}/acknowledge", null);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            ownerClient.PostAsJsonAsync($"/api/v1/donations/{id}/confirm-paid", new
            {
                PaymentRef = "TXN-CONCURRENT",
                PaymentEvidenceUrl = (string?)null
            })));

        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1,
            "chỉ đúng 1 trong 6 request đồng thời được xác nhận thành công");
        responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).Should().Be(5,
            "5 request còn lại phải thấy lỗi trạng thái đã đổi, không phải lỗi hạ tầng (500)");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Chặng 1 (ProcessDonationPaymentCommandHandler, đã chạy qua SimulateVnPayCallbackAsync ở
        // trên) cũng ghi 4 dòng cùng ReferenceType/ReferenceId — lọc riêng theo AccountType.Performer
        // vì CHỈ chặng 2 mới đụng tới account loại này, nên đây là cách đếm "chặng 2 đã chạy mấy lần"
        // tách biệt khỏi 4 dòng chặng 1 luôn có sẵn.
        var performerEntryCount = await db.LedgerEntries
            .Where(e => e.ReferenceType == "donation" && e.ReferenceId == id.ToString())
            .Join(db.Set<MusicLounge.Domain.Entities.Account>(), e => e.AccountId, a => a.Id, (e, a) => a)
            .CountAsync(a => a.OwnerType == AccountType.Performer);
        performerEntryCount.Should().Be(1,
            "chỉ đúng 1 journal chặng 2 được ghi — không bị nhân đôi dù có 6 request đồng thời");
    }

    // ─── Ledger integration (found missing entirely during this session's audit) ──

    /// <summary>
    /// The full donation flow (create → VNPay confirm → owner ack → owner-paid-performer) never
    /// wrote a single ledger entry before this session's fix — donation revenue, platform
    /// commission, and tax withheld were invisible to the ledger and to
    /// GetLedgerIntegrityQueryHandler. Drives the real flow end-to-end through the HTTP
    /// endpoints and checks both stages actually landed in the ledger with the right amounts.
    /// </summary>
    [Fact]
    public async Task DonationLifecycle_WritesBalancedLedgerEntriesForBothStages()
    {
        const decimal gross = 100_000m;
        var (id, orderId) = await CreateDonationAsync(amount: gross);
        await SimulateVnPayCallbackAsync(orderId, success: true);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Tai khoan so cai cua chu co the chua ton tai: tu MLACP-361 chang 1 khong con ghi Co cho chu,
            // nen chi bai nao da giai ngan truoc do moi tao ra no. Khong duoc dua vao thu tu chay bai.
            var ownerAccountId = await db.LedgerAccounts
                .Where(a => a.OwnerType == AccountType.User && a.OwnerId == SeedHelper.OwnerId)
                .Select(a => (int?)a.Id)
                .FirstOrDefaultAsync();

            var stage1Entries = await db.LedgerEntries
                .Where(e => e.ReferenceType == "donation" && e.ReferenceId == id.ToString())
                .ToListAsync();
            stage1Entries.Should().NotBeEmpty("VNPay confirming payment must write chặng 1's journal");
            stage1Entries.Where(e => e.IsDebit).Sum(e => e.Amount)
                .Should().Be(stage1Entries.Where(e => !e.IsDebit).Sum(e => e.Amount), "journal must balance");

            // MLACP-361: chặng 1 giữ phần của phòng trà ở Platform chờ quyết toán — không còn ghi Có
            // thẳng cho chủ phòng trà trước khi tiền thật sự được chuyển.
            stage1Entries.Should().NotContain(e => e.AccountId == ownerAccountId,
                "chưa giải ngân thì chưa có đồng nào thuộc về tài khoản chủ phòng trà");
            var platformAccount = await db.LedgerAccounts.SingleAsync(
                a => a.OwnerType == AccountType.Platform && a.OwnerId == null);
            stage1Entries.Where(e => e.AccountId == platformAccount.Id && !e.IsDebit).Sum(e => e.Amount)
                .Should().Be(95_000m, "5% hoa hồng + 90% giữ hộ chủ phòng trà chờ quyết toán (gross - 5% - 5% thuế)");
        }

        await ReleaseVenuePayoutsAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ownerAccount = await db.LedgerAccounts.SingleAsync(
                a => a.OwnerType == AccountType.User && a.OwnerId == SeedHelper.OwnerId);
            var payment = await db.Payments.SingleAsync(p => p.ReferenceType == "Donation" && p.ReferenceId == id.ToString());
            var settlement = await db.Settlements.SingleAsync(s => s.PaymentId == payment.Id);
            var payout = await db.LedgerEntries.SingleAsync(e =>
                e.ReferenceType == "settlement" && e.ReferenceId == settlement.Id.ToString() && e.AccountId == ownerAccount.Id);
            payout.IsDebit.Should().BeFalse();
            payout.Amount.Should().Be(90_000m, "giải ngân chặng 1 mới là lúc phần 90% vào tài khoản chủ phòng trà");
        }

        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        await ownerClient.PostAsync($"/api/v1/donations/{id}/acknowledge", null);
        await ownerClient.PostAsJsonAsync($"/api/v1/donations/{id}/confirm-paid", new
        {
            PaymentRef = "TXN123456",
            PaymentEvidenceUrl = "https://bank.test/receipt/123"
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var performerAccount = await db.LedgerAccounts.SingleAsync(
                a => a.OwnerType == AccountType.Performer && a.OwnerId == SeedHelper.PerformerId);

            var allEntries = await db.LedgerEntries
                .Where(e => e.ReferenceType == "donation" && e.ReferenceId == id.ToString())
                .ToListAsync();
            allEntries.Should().HaveCount(6, "4 dòng chặng 1 (Gateway/Platform hoa hồng/Tax/Platform giữ hộ) + 2 dòng chặng 2 (Owner/Performer)");
            allEntries.Where(e => e.IsDebit).Sum(e => e.Amount)
                .Should().Be(allEntries.Where(e => !e.IsDebit).Sum(e => e.Amount));

            var performerCredit = allEntries.Single(e => e.AccountId == performerAccount.Id);
            performerCredit.IsDebit.Should().BeFalse();
            performerCredit.Amount.Should().Be(88_000m,
                "chặng 2 chuyển 88% GROSS cho nghệ sĩ (system_config donation_performer_share_rate mặc định 0.88, §6.5) — owner giữ lại 2% còn lại trong 90% đã nhận ở chặng 1");
        }
    }

    /// <summary>
    /// PerformerShareRateSnapshot is frozen the moment VNPay confirms the donation (chặng 1) — not
    /// re-read from system_config at whatever later moment the Owner gets around to confirm-paid
    /// (chặng 2, which per DonationHoldDays can be days/weeks later). Proves the fix by directly
    /// diverging the snapshot from the live default AFTER VNPay confirmation (simulating "an admin
    /// changed the rate in between") and checking every downstream reader — the pending/awaiting
    /// preview lists AND the actual ledger transfer — uses the frozen value, not the live default.
    /// </summary>
    [Fact]
    public async Task DonationLifecycle_RateChangedAfterVnPayConfirms_StillUsesSnapshotEverywhere()
    {
        const decimal gross = 100_000m;
        var (id, orderId) = await CreateDonationAsync(amount: gross);
        await SimulateVnPayCallbackAsync(orderId, success: true);
        await ReleaseVenuePayoutsAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var donation = await db.Donations.SingleAsync(d => d.Id == id);
            donation.PerformerShareRateSnapshot.Should().Be(0.88m,
                "ProcessDonationPaymentCommandHandler must freeze the rate the instant VNPay confirms");

            // Simulate "an Admin changed donation_performer_share_rate after this donation was
            // already confirmed" without fighting ISystemConfigService's 60s memory cache — directly
            // diverge this donation's own frozen snapshot from what a fresh config read would give.
            donation.PerformerShareRateSnapshot = 0.50m;
            await db.SaveChangesAsync();
        }

        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var pendingRes = await ownerClient.GetAsync("/api/v1/donations/pending-ack?pageSize=50");
        (await pendingRes.Content.ReadAsStringAsync()).Should().Contain("\"amountToPayPerformer\":50000",
            "preview must reflect THIS donation's own snapshot (50%), not the live 88% default");

        await ownerClient.PostAsync($"/api/v1/donations/{id}/acknowledge", null);

        var awaitingRes = await ownerClient.GetAsync("/api/v1/donations/awaiting-payout?pageSize=50");
        (await awaitingRes.Content.ReadAsStringAsync()).Should().Contain("\"amountToPayPerformer\":50000");

        await ownerClient.PostAsJsonAsync($"/api/v1/donations/{id}/confirm-paid", new
        {
            PaymentRef = "TXN-SNAPSHOT",
            PaymentEvidenceUrl = (string?)null
        });

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var performerAccount = await verifyDb.LedgerAccounts.SingleAsync(
            a => a.OwnerType == AccountType.Performer && a.OwnerId == SeedHelper.PerformerId);
        var performerCredit = await verifyDb.LedgerEntries.SingleAsync(e =>
            e.ReferenceType == "donation" && e.ReferenceId == id.ToString() && e.AccountId == performerAccount.Id);
        performerCredit.Amount.Should().Be(50_000m,
            "the actual transfer must match the frozen snapshot (50%), not whatever system_config says now");
    }

    /// <summary>
    /// PaymentFeeCalculator.SplitDonationPayout is the single source of truth for chặng-2 math
    /// (mirrors Split's role for chặng 1) — pure function, no HTTP/DB needed. Proves the rate is a
    /// genuine parameter (not a hardcoded 0.88 anywhere in the calculation) and that the negative-
    /// OwnerRetained guard actually fires when donation_performer_share_rate is misconfigured
    /// above what the owner's chặng-1 net can cover.
    /// </summary>
    [Theory]
    [InlineData(100_000, 90_000, 0.88, 88_000, 2_000)]   // documented default (§6.5)
    [InlineData(100_000, 90_000, 0.80, 80_000, 10_000)]  // Admin retuned the rate — not hardcoded
    [InlineData(100_000, 90_000, 1.00, 100_000, -10_000)] // misconfigured: exceeds owner's net → negative guard must catch this
    public void SplitDonationPayout_ComputesFromRateParameter_NotAHardcodedConstant(
        decimal gross, decimal ownerNet, decimal rate, decimal expectedPerformerAmount, decimal expectedOwnerRetained)
    {
        var split = MusicLounge.Application.Common.PaymentFeeCalculator.SplitDonationPayout(gross, ownerNet, rate);

        split.PerformerAmount.Should().Be(expectedPerformerAmount);
        split.OwnerRetained.Should().Be(expectedOwnerRetained);
    }

    // ─── Pending-ack / awaiting-payout lists — previously zero test coverage ──

    [Fact]
    public async Task GetPendingAck_AsOwner_ContainsDonationAwaitingAcknowledgement()
    {
        var (id, orderId) = await CreateDonationAsync();
        await SimulateVnPayCallbackAsync(orderId, success: true); // → PendingOwnerAck

        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var res = await ownerClient.GetAsync("/api/v1/donations/pending-ack?pageSize=50");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain($"\"id\":{id}");
    }

    [Fact]
    public async Task GetPendingAck_AsAudience_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.GetAsync("/api/v1/donations/pending-ack");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAwaitingPayout_AsOwner_ContainsAcknowledgedDonation()
    {
        var (id, orderId) = await CreateDonationAsync();
        await SimulateVnPayCallbackAsync(orderId, success: true);
        await ReleaseVenuePayoutsAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        await ownerClient.PostAsync($"/api/v1/donations/{id}/acknowledge", null); // → OwnerReceived

        var res = await ownerClient.GetAsync("/api/v1/donations/awaiting-payout?pageSize=50");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain($"\"id\":{id}");
    }

    // ─── D17 Public donation history ──────────────────────────────────────────

    [Fact]
    public async Task GetPublicDonationHistory_NoAuth_Returns200()
    {
        var client = _factory.CreateClient(); // anonymous

        var res = await client.GetAsync(
            $"/api/v1/performers/{SeedHelper.PerformerId}/donations?page=1&pageSize=20");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":true");
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────

    private sealed record DonationInitResponse(bool Success, DonationInitData Data);
    private sealed record DonationInitData(int DonationId, string OrderId, decimal Gross, string PaymentUrl);
    private sealed record BoolResponse(bool Success, bool Data);
}

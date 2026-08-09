using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.E2E;

/// <summary>
/// Nối liền thật sự — không seed DB tắt cho bất kỳ bước nghiệp vụ nào — luồng người dùng thật từ
/// đầu đến cuối mà mọi CF1-CF7 test khác đều bỏ qua (mỗi CF chỉ test 1 lát cắt, tự bơm thẳng
/// precondition vào DB). Test này gọi tuần tự đúng chuỗi API thật một Owner sẽ đi qua:
///
/// Register(Role=Owner) → VerifyEmail → Login → CreateLounge → Subscribe(VNPay) →
/// CreateLoungeShow → CreateTicketTier → SetLegalApproval → Publish (Draft→Pending) →
/// Admin ReviewShow Approve (Pending→Published) → show xuất hiện trên GET /lounge-shows
/// (homepage feed công khai) → AssignStaff → Staff bán vé walk-in → vé Confirmed.
///
/// Giới hạn có chủ đích: bước xác thực (Authorization header) từ bước 5 trở đi dùng
/// CreateAuthenticatedClient (TestAuthHandler) thay vì gắn thật JWT vừa nhận — vì ApiFactory đổi
/// DefaultAuthenticateScheme sang "Test" cho toàn bộ test host (xem ApiFactory.cs), nên 1 JWT thật
/// gắn vào Authorization header sẽ không được middleware nào đọc tới. Đây là giới hạn hạ tầng test
/// dùng chung với 214 test khác, không phải shortcut nghiệp vụ — mọi entity (Lounge, Subscription,
/// Show, TicketTier, Ticket...) trong test này đều được tạo qua đúng API thật, không insert thẳng DB.
/// </summary>
[Collection("Integration")]
public sealed class OwnerGoldenPathTests
{
    private readonly ApiFactory _factory;

    public OwnerGoldenPathTests(ApiFactory factory) => _factory = factory;

    private static string HashCode(string rawCode)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawCode)));

    [Fact]
    public async Task Owner_FullJourney_RegisterToPublishedShowToSoldTicket_Succeeds()
    {
        var anon = _factory.CreateClient();
        var email = $"golden-{Guid.NewGuid():N}@test.com";

        // ── 1. Register as Owner (self-registration explicitly allows "Owner", see
        //       RegisterCommandValidator) ──────────────────────────────────────────────
        var registerRes = await anon.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = "P@ssword123", FullName = "Golden Path Owner",
            Phone = (string?)null, Role = "Owner", AcceptTerms = true
        });
        registerRes.StatusCode.Should().Be(HttpStatusCode.OK, "register with Role=Owner must be accepted");

        // ── 2. Verify email (OTP delivery is a real email in prod — here we stand in for the
        //       inbox exactly like AuthTests does: write a known code's hash, same as the
        //       handler itself would after generating a real code) ───────────────────────
        const string rawCode = "654321";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.EmailVerificationCodeHash = HashCode(rawCode);
            user.EmailVerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
            await db.SaveChangesAsync();
        }

        var verifyRes = await anon.PostAsJsonAsync(
            "/api/v1/auth/verify-email", new { Email = email, Code = rawCode });
        verifyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var verifyBody = await verifyRes.Content.ReadFromJsonAsync<AuthResponse>();
        verifyBody!.Data.Role.Should().Be("Owner");
        var ownerId = verifyBody.Data.UserId;

        // ── 3. Login confirms the same account, real password check included ──────────
        var loginRes = await anon.PostAsJsonAsync(
            "/api/v1/auth/login", new { Email = email, Password = "P@ssword123" });
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();
        loginBody!.Data.UserId.Should().Be(ownerId);
        loginBody.Data.Role.Should().Be("Owner");

        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        // ── 4. Create venue ────────────────────────────────────────────────────────────
        var loungeRes = await ownerClient.PostAsJsonAsync("/api/v1/lounges", new
        {
            Name = "Golden Path Lounge", Description = "E2E", AtmosphereId = (int?)null,
            Street = "1 Golden St", Ward = "Ward 1", District = "District 1", City = "HCM",
            Latitude = (double?)null, Longitude = (double?)null
        });
        loungeRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var loungeId = (await loungeRes.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;

        // ── 5. Subscribe to an active package via real VNPay callback simulation
        //       (FakeVnPayService — same mechanism SubscriptionTests already proves works) ──
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var packageRes = await adminClient.PostAsJsonAsync("/api/v1/subscriptions/packages", new
        {
            Name = $"GoldenPkg-{Guid.NewGuid():N}", Description = "E2E package", Price = 250_000m,
            BillingCycle = "Monthly", MaxTicketsPerEvent = 100, HasAiPoster = true
        });
        packageRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var packageId = (await packageRes.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;

        var subscribeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        subscribeRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var initiation = await subscribeRes.Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();

        var noRedirect = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var callbackRes = await noRedirect.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation.Data.Amount * 100)}");
        callbackRes.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var mySubRes = await ownerClient.GetAsync("/api/v1/subscriptions/my");
        (await mySubRes.Content.ReadAsStringAsync()).Should().Contain("\"status\":\"Active\"");

        // ── 6. Create the show (Draft) ─────────────────────────────────────────────────
        var showRes = await ownerClient.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = loungeId, Name = "Golden Path Show", Description = "E2E show",
            Format = "Offline", ScheduledStart = DateTimeOffset.UtcNow.AddDays(14),
            ScheduledEnd = (DateTimeOffset?)null, CategoryId = (int?)null,
            OfflineQuota = 100, OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(), Performances = Array.Empty<object>()
        });
        showRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var showId = (await showRes.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;

        // ── 7. Add a ticket tier (required before publish) ─────────────────────────────
        var tierRes = await ownerClient.PostAsJsonAsync("/api/v1/ticket-tiers", new
        {
            ShowId = showId, Name = "Standard", Description = (string?)null, AccessType = "Physical",
            ZoneId = (int?)null, TotalCapacity = 100,
            Prices = new[]
            {
                new
                {
                    Name = "Standard Price", Price = 150_000m, Quota = (int?)50,
                    PurchaseChannel = "Both", SaleStart = DateTimeOffset.UtcNow,
                    SaleEnd = DateTimeOffset.UtcNow.AddDays(13)
                }
            }
        });
        tierRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // ── 8. Legal approval reference (required before publish, NĐ 144/2020 Điều 10) ──
        var legalRes = await ownerClient.PutAsJsonAsync(
            $"/api/v1/lounge-shows/{showId}/legal-approval",
            new { LegalApprovalReference = "SoVHTT-GOLDEN-0001" });
        legalRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // ── 9. Publish = submit for moderation (Draft → Pending, NOT visible yet) ──────
        var publishRes = await ownerClient.PostAsync($"/api/v1/lounge-shows/{showId}/publish", null);
        publishRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var beforeApprovalListing = await anon.GetAsync("/api/v1/lounge-shows?pageSize=100");
        (await beforeApprovalListing.Content.ReadAsStringAsync())
            .Should().NotContain($"\"id\":{showId}", "Pending show must NOT be visible on the public homepage feed yet");

        // ── 10. Admin approves (Pending → Published) ───────────────────────────────────
        var reviewRes = await adminClient.PostAsJsonAsync(
            $"/api/v1/moderations/shows/{showId}/review",
            new { Decision = "Approved", ReviewNote = "Looks good" });
        reviewRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // ── 11. Show now appears on the public homepage feed — the exact promise this whole
        //        chain exists to prove ─────────────────────────────────────────────────────
        var afterApprovalListing = await anon.GetAsync("/api/v1/lounge-shows?pageSize=100");
        (await afterApprovalListing.Content.ReadAsStringAsync())
            .Should().Contain($"\"id\":{showId}", "Published show must be visible on the public homepage feed");

        // ── 12. Assign staff to the new venue — a fresh user, not SeedHelper.AudienceId:
        //        that shared seeded user must stay Audience-with-no-active-assignment for OTHER
        //        CF1 tests (EventManagementTests.AssignStaff_UserAlreadyActiveAtAnotherVenue_Returns409
        //        relies on it), same isolation rule VenuePenaltyTests documents for itself ──────
        int staffUserId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var staffCandidate = new MusicLounge.Domain.Entities.User
            {
                Email = $"golden-staff-{Guid.NewGuid():N}@test.com", FullName = "Golden Path Staff"
            };
            db.Users.Add(staffCandidate);
            await db.SaveChangesAsync();
            staffUserId = staffCandidate.Id;
        }

        var assignRes = await ownerClient.PostAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/staff", new { UserId = staffUserId });
        assignRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // ── 13. Staff sells a walk-in ticket against the real tier/price just created ──
        var tierDetailRes = await anon.GetAsync($"/api/v1/ticket-tiers?showId={showId}");
        var tierDetailBody = await tierDetailRes.Content.ReadAsStringAsync();
        var priceId = ExtractFirstPriceId(tierDetailBody);

        var staffClient = _factory.CreateAuthenticatedClient(staffUserId, "Staff", loungeId);
        var walkInRes = await staffClient.PostAsJsonAsync(
            "/api/v1/tickets/walk-in", new { PriceId = priceId, Quantity = 1 });
        walkInRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // ── 14. Final integrity check straight from the DB: the sold ticket really is
        //        wired to the exact lounge/show/owner created earlier in this same chain ──
        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var soldTicket = await finalDb.Tickets
            .Where(t => t.ShowId == showId && t.Status == TicketStatus.Confirmed)
            .SingleAsync();
        var show = await finalDb.LoungeShows.SingleAsync(s => s.Id == showId);
        show.LoungeId.Should().Be(loungeId);
        var lounge = await finalDb.Lounges.SingleAsync(l => l.Id == loungeId);
        lounge.OwnerId.Should().Be(ownerId);
        soldTicket.ShowId.Should().Be(showId);
    }

    private static int ExtractFirstPriceId(string tierListJson)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(tierListJson);
        var prices = doc.RootElement.GetProperty("data")[0].GetProperty("prices");
        return prices[0].GetProperty("id").GetInt32();
    }

    private sealed record AuthResponse(bool Success, AuthResultData Data);
    private sealed record AuthResultData(string Token, DateTimeOffset ExpiresAt, int UserId, string Email, string FullName, string Role);
    private sealed record DataResponse<T>(bool Success, T Data);
    private sealed record SubscriptionInitiationData(int PaymentId, string OrderId, decimal Amount, string PaymentUrl);
    private sealed record SubscriptionInitiationResponse(bool Success, SubscriptionInitiationData Data);
}

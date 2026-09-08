using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-290, closing finding R7 of the đợt-0 audit. A user could submit a citizen card and an Admin
/// could look at the images — and that was the entire workflow. Nothing accepted a document, nothing
/// refused one, and no column recorded what anyone had concluded. Submissions went in and stopped.
///
/// MLACP-289 turned that from a procedural gap into a financial one: declaring yourself a doanh
/// nghiệp is the only thing that stops the platform withholding tax, and it deliberately takes
/// effect only once approved. With no way to approve anything, the exemption existed and could never
/// be reached by anyone, including a genuine company.
/// </summary>
[Collection("Integration")]
public sealed class KycReviewTests
{
    private readonly ApiFactory _factory;

    public KycReviewTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    /// <summary>A fresh seller each time, so one test's decisions cannot colour another's queue.</summary>
    private async Task<int> SeedSellerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"kyc-{Guid.NewGuid():N}@test.com",
            FullName = "Chủ Phòng Trà Chờ Duyệt",
            Role = UserRole.Owner,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<User> ReadUserAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.SingleAsync(u => u.Id == userId);
    }

    private async Task DeclareTaxProfileAsync(int userId, string businessType, string taxCode)
    {
        var client = _factory.CreateAuthenticatedClient(userId, "Owner");
        var res = await client.PutAsJsonAsync("/api/v1/me/tax-profile",
            new { BusinessType = businessType, TaxCode = taxCode });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A random valid-format tax code, so parallel seeds cannot collide on the unique index.</summary>
    private static string NewTaxCode() => Random.Shared.NextInt64(1_000_000_000L, 9_999_999_999L).ToString();

    [Fact]
    public async Task ApprovingAnEnterpriseTaxProfile_IsWhatActuallyStopsWithholding()
    {
        var userId = await SeedSellerAsync();
        await DeclareTaxProfileAsync(userId, "Enterprise", NewTaxCode());

        (await ReadUserAsync(userId)).TaxProfileVerifiedAt.Should().BeNull(
            "declaring is not being believed — this is the state MLACP-289 leaves a declaration in");

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{userId}/TaxProfile",
            new { Approve = true, Note = "Đã đối chiếu giấy đăng ký kinh doanh" });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reviewed = await ReadUserAsync(userId);
        reviewed.TaxProfileReviewStatus.Should().Be(KycReviewStatus.Approved);
        reviewed.TaxProfileVerifiedAt.Should().NotBeNull(
            "this timestamp is the single thing TaxWithholdingPolicy reads — without it the " +
            "exemption is unreachable no matter what else the review recorded");
        reviewed.TaxProfileVerifiedBy.Should().Be(SeedHelper.AdminId);
    }

    [Fact]
    public async Task RejectingAPreviouslyApprovedProfile_PutsWithholdingBackOn()
    {
        var userId = await SeedSellerAsync();
        await DeclareTaxProfileAsync(userId, "Enterprise", NewTaxCode());

        await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{userId}/TaxProfile",
            new { Approve = true, Note = (string?)null });
        (await ReadUserAsync(userId)).TaxProfileVerifiedAt.Should().NotBeNull();

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{userId}/TaxProfile",
            new { Approve = false, Note = "Giấy tờ doanh nghiệp không khớp mã số thuế đã khai" });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reviewed = await ReadUserAsync(userId);
        reviewed.TaxProfileReviewStatus.Should().Be(KycReviewStatus.Rejected);
        reviewed.TaxProfileVerifiedAt.Should().BeNull(
            "leaving the old approval behind would quietly keep exempting a seller whose " +
            "declaration was just turned down");
        reviewed.TaxProfileVerifiedBy.Should().BeNull();
    }

    [Fact]
    public async Task RejectionWithoutAReason_IsRefused()
    {
        var userId = await SeedSellerAsync();
        await DeclareTaxProfileAsync(userId, "HouseholdOrIndividual", NewTaxCode());

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{userId}/TaxProfile",
            new { Approve = false, Note = "" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "\"rejected\" on its own tells the submitter to guess what to fix");
    }

    [Fact]
    public async Task RejectedSeller_IsToldWhyAndCanSeeItOnTheirOwnProfile()
    {
        var userId = await SeedSellerAsync();
        await DeclareTaxProfileAsync(userId, "Enterprise", NewTaxCode());

        await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{userId}/TaxProfile",
            new { Approve = false, Note = "Mã số thuế không tồn tại trên hệ thống thuế" });

        var profile = (await (await _factory.CreateAuthenticatedClient(userId, "Owner")
                .GetAsync("/api/v1/me/tax-profile"))
            .Content.ReadFromJsonAsync<Envelope<TaxProfile>>())!.Data;

        profile.ReviewStatus.Should().Be("Rejected");
        profile.ReviewNote.Should().Be("Mã số thuế không tồn tại trên hệ thống thuế");
        profile.WithholdingApplies.Should().BeTrue();
        profile.Explanation.Should().Contain("bị từ chối").And.Contain("nộp lại");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notified = await db.Notifications.AnyAsync(
            n => n.UserId == userId && n.Type == NotificationType.KycReviewResult);
        notified.Should().BeTrue(
            "the submitter has no other way to learn the outcome — they hand over documents and " +
            "otherwise hear nothing back");
    }

    [Fact]
    public async Task ResubmittingAfterRejection_ReopensTheReview()
    {
        var userId = await SeedSellerAsync();
        await DeclareTaxProfileAsync(userId, "Enterprise", NewTaxCode());
        await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{userId}/TaxProfile",
            new { Approve = false, Note = "Thiếu giấy đăng ký kinh doanh" });

        await DeclareTaxProfileAsync(userId, "Enterprise", NewTaxCode());

        var reviewed = await ReadUserAsync(userId);
        reviewed.TaxProfileReviewStatus.Should().Be(KycReviewStatus.Pending,
            "a rejection has to be recoverable, or one bad submission locks the seller out for good");
        reviewed.TaxProfileReviewNote.Should().BeNull("the old reason no longer describes what was sent");
    }

    [Fact]
    public async Task Queue_ShowsWaitingSubmissions_AndFlagsTheOnesThatChangeMoney()
    {
        var householdId = await SeedSellerAsync();
        var enterpriseId = await SeedSellerAsync();
        await DeclareTaxProfileAsync(householdId, "HouseholdOrIndividual", NewTaxCode());
        await DeclareTaxProfileAsync(enterpriseId, "Enterprise", NewTaxCode());

        var res = await Admin().GetAsync("/api/v1/admin/kyc-reviews?status=Pending&pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var queue = (await res.Content.ReadFromJsonAsync<Envelope<Paged<QueueItem>>>())!.Data.Items;

        var household = queue.Single(i => i.UserId == householdId);
        var enterprise = queue.Single(i => i.UserId == enterpriseId);

        household.WithholdingWouldStopIfApproved.Should().BeFalse();
        enterprise.WithholdingWouldStopIfApproved.Should().BeTrue(
            "approving this one stops deducting tax from a seller — it should not look like the " +
            "routine case sitting next to it in the list");

        // Deciding it removes it from the pending queue rather than leaving it there forever.
        await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{householdId}/TaxProfile",
            new { Approve = true, Note = (string?)null });

        var after = (await (await Admin().GetAsync("/api/v1/admin/kyc-reviews?status=Pending&pageSize=100"))
            .Content.ReadFromJsonAsync<Envelope<Paged<QueueItem>>>())!.Data.Items;
        after.Should().NotContain(i => i.UserId == householdId);
    }

    [Fact]
    public async Task ReviewingSomethingNeverSubmitted_IsRefused()
    {
        var userId = await SeedSellerAsync();

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{userId}/CitizenCard",
            new { Approve = true, Note = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("chưa nộp");
    }

    [Fact]
    public async Task OnlyAdminsCanDecide()
    {
        var userId = await SeedSellerAsync();
        await DeclareTaxProfileAsync(userId, "Enterprise", NewTaxCode());

        var self = _factory.CreateAuthenticatedClient(userId, "Owner");

        (await self.GetAsync("/api/v1/admin/kyc-reviews")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await self.PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{userId}/TaxProfile",
                new { Approve = true, Note = "Tự duyệt cho chính mình" })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden,
                "otherwise the exemption is self-service through a different door");
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
    private sealed record QueueItem(
        int UserId, string FullName, string Email, string? CitizenCardNumberMasked,
        DateTimeOffset? CitizenCardSubmittedAt, string? CitizenCardReviewStatus,
        string? BusinessType, string? TaxCode, DateTimeOffset? TaxProfileSubmittedAt,
        string? TaxProfileReviewStatus, bool WithholdingWouldStopIfApproved);
    private sealed record TaxProfile(
        string? BusinessType, string? TaxCode, DateTimeOffset? SubmittedAt, DateTimeOffset? VerifiedAt,
        string? ReviewStatus, string? ReviewNote, bool WithholdingApplies,
        decimal VatRate, decimal PersonalIncomeTaxRate, string Explanation);
}

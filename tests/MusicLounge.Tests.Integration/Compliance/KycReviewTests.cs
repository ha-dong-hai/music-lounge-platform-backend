using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLounge.Domain.ValueObjects;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

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

    /// <summary>MLACP-398: doanh nghiệp phải khai tên doanh nghiệp.</summary>
    private const string EnterpriseName = "CÔNG TY TNHH PHÒNG TRÀ THỬ NGHIỆM";

    /// <summary>A fresh seller each time, so one test's decisions cannot colour another's queue.</summary>
    /// <param name="cardApproved">MLACP-398: CCCD/CMND của người đại diện đã được duyệt.</param>
    /// <param name="businessLicence">MLACP-398: phòng trà của người bán đã nộp giấy chứng nhận đăng ký kinh doanh.</param>
    private async Task<int> SeedSellerAsync(bool cardApproved = false, bool businessLicence = false)
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
            IsActive = true,
            CitizenCardSubmittedAt = cardApproved ? DateTimeOffset.UtcNow.AddDays(-1) : null,
            CitizenCardReviewStatus = cardApproved ? KycReviewStatus.Approved : null
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        if (businessLicence)
        {
            db.Add(new MusicLoungeVenue
            {
                OwnerId = user.Id, Name = $"Kyc398 {Guid.NewGuid():N}"[..20], Status = LoungeStatus.Approved,
                BusinessLicenseUrl = $"private/licence-{Guid.NewGuid():N}.pdf",
                Address = new VenueAddress { Street = "1 Lê Lợi", District = "1", City = "HCM" }
            });
            await db.SaveChangesAsync();
        }

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
            new { BusinessType = businessType, TaxCode = taxCode, LegalName = businessType == "Enterprise" ? EnterpriseName : (string?)null });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A random valid-format tax code, so parallel seeds cannot collide on the unique index.</summary>
    private static string NewTaxCode() => Random.Shared.NextInt64(1_000_000_000L, 9_999_999_999L).ToString();

    [Fact]
    public async Task ApprovingAnEnterpriseTaxProfile_IsWhatActuallyStopsWithholding()
    {
        var userId = await SeedSellerAsync(cardApproved: true, businessLicence: true);
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
        var userId = await SeedSellerAsync(cardApproved: true, businessLicence: true);
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
    public async Task Queue_ShowsTheDateOfBirthToCompareWithTheCard()
    {
        // MLACP-397: Admin đối chiếu họ tên, ngày sinh và số giấy tờ với ảnh CCCD/CMND — không thấy ngày sinh thì không
        // đối chiếu được. Nộp từ lâu để hồ sơ nằm đầu hàng đợi (sắp theo thời điểm nộp), không rơi khỏi trang đầu.
        var userId = await SeedSellerAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.DateOfBirth = new DateOnly(1985, 3, 9);
            user.CitizenCardSubmittedAt = DateTimeOffset.UtcNow.AddYears(-20);
            user.CitizenCardReviewStatus = KycReviewStatus.Pending;
            await db.SaveChangesAsync();
        }

        var body = await (await Admin().GetAsync("/api/v1/admin/kyc-reviews?status=Pending&pageSize=100"))
            .Content.ReadAsStringAsync();
        using var json = System.Text.Json.JsonDocument.Parse(body);
        var item = json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("userId").GetInt32() == userId);

        item.GetProperty("dateOfBirth").GetString().Should().Be("1985-03-09");
    }

    // ---------- MLACP-398: hồ sơ doanh nghiệp ----------

    [Fact]
    public async Task DeclaringAnEnterprise_WithoutItsLegalName_IsRefused()
    {
        // Tên doanh nghiệp là thứ Admin đối chiếu với giấy chứng nhận đăng ký kinh doanh.
        var userId = await SeedSellerAsync();

        var res = await _factory.CreateAuthenticatedClient(userId, "Owner").PutAsJsonAsync("/api/v1/me/tax-profile",
            new { BusinessType = "Enterprise", TaxCode = NewTaxCode() });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("tên doanh nghiệp");
        (await ReadUserAsync(userId)).TaxProfileSubmittedAt.Should().BeNull();
    }

    [Fact]
    public async Task DeclaringAHousehold_DoesNotKeepALegalName()
    {
        var userId = await SeedSellerAsync();
        var taxCode = NewTaxCode();
        await DeclareTaxProfileAsync(userId, "Enterprise", taxCode);
        (await ReadUserAsync(userId)).LegalName.Should().Be(EnterpriseName);

        var res = await _factory.CreateAuthenticatedClient(userId, "Owner").PutAsJsonAsync("/api/v1/me/tax-profile",
            new { BusinessType = "HouseholdOrIndividual", TaxCode = taxCode, LegalName = EnterpriseName });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ReadUserAsync(userId)).LegalName.Should().BeNull("tên doanh nghiệp chỉ có nghĩa với doanh nghiệp");
    }

    [Fact]
    public async Task ChangingTheLegalName_DropsTheVerification()
    {
        var userId = await SeedSellerAsync(cardApproved: true, businessLicence: true);
        var taxCode = NewTaxCode();
        await DeclareTaxProfileAsync(userId, "Enterprise", taxCode);
        (await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{userId}/TaxProfile", new { Approve = true, Note = (string?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadUserAsync(userId)).TaxProfileVerifiedAt.Should().NotBeNull();

        var res = await _factory.CreateAuthenticatedClient(userId, "Owner").PutAsJsonAsync("/api/v1/me/tax-profile",
            new { BusinessType = "Enterprise", TaxCode = taxCode, LegalName = "CÔNG TY CỔ PHẦN TÊN KHÁC" });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var user = await ReadUserAsync(userId);
        user.LegalName.Should().Be("CÔNG TY CỔ PHẦN TÊN KHÁC");
        user.TaxProfileVerifiedAt.Should().BeNull(
            "tên đã duyệt là tên đã được đối chiếu với giấy chứng nhận — tên mới thì chưa ai đối chiếu");
    }

    [Fact]
    public async Task ApprovingAnEnterprise_WhoseRepresentativeIsNotVerified_IsRefused()
    {
        var userId = await SeedSellerAsync(cardApproved: false, businessLicence: true);
        await DeclareTaxProfileAsync(userId, "Enterprise", NewTaxCode());

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{userId}/TaxProfile", new { Approve = true, Note = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("người đại diện");
        (await ReadUserAsync(userId)).TaxProfileVerifiedAt.Should().BeNull(
            "không được thôi khấu trừ thuế cho một tổ chức mà chưa ai xác minh người đại diện");
    }

    [Fact]
    public async Task ApprovingAnEnterprise_WithoutABusinessLicence_IsRefused()
    {
        var userId = await SeedSellerAsync(cardApproved: true, businessLicence: false);
        await DeclareTaxProfileAsync(userId, "Enterprise", NewTaxCode());

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{userId}/TaxProfile", new { Approve = true, Note = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("giấy chứng nhận đăng ký kinh doanh");
        (await ReadUserAsync(userId)).TaxProfileVerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task ApprovingAnEnterprise_ThatNeverGaveItsLegalName_IsRefused()
    {
        // Hồ sơ doanh nghiệp khai trước MLACP-398 không có tên doanh nghiệp.
        var userId = await SeedSellerAsync(cardApproved: true, businessLicence: true);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.BusinessType = PayeeBusinessType.Enterprise;
            user.LegalName = null;
            user.TaxProfileSubmittedAt = DateTimeOffset.UtcNow;
            user.TaxProfileReviewStatus = KycReviewStatus.Pending;
            await db.SaveChangesAsync();
        }

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{userId}/TaxProfile", new { Approve = true, Note = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("tên doanh nghiệp");
        (await ReadUserAsync(userId)).TaxProfileVerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task ApprovingAHousehold_NeedsNeitherTheLicenceNorTheRepresentativeCheck()
    {
        var userId = await SeedSellerAsync();
        await DeclareTaxProfileAsync(userId, "HouseholdOrIndividual", NewTaxCode());

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{userId}/TaxProfile", new { Approve = true, Note = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "duyệt hồ sơ hộ/cá nhân không đổi việc khấu trừ thuế, và không cần giấy tờ của doanh nghiệp");
    }

    [Fact]
    public async Task QueueAndOwnProfile_ShowTheLegalName_AndWhetherALicenceIsOnFile()
    {
        var withLicence = await SeedSellerAsync(cardApproved: true, businessLicence: true);
        var withoutLicence = await SeedSellerAsync();
        await DeclareTaxProfileAsync(withLicence, "Enterprise", NewTaxCode());
        await DeclareTaxProfileAsync(withoutLicence, "Enterprise", NewTaxCode());

        var body = await (await Admin().GetAsync("/api/v1/admin/kyc-reviews?status=Pending&pageSize=100"))
            .Content.ReadAsStringAsync();
        using var json = System.Text.Json.JsonDocument.Parse(body);
        var items = json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        var licensed = items.Single(i => i.GetProperty("userId").GetInt32() == withLicence);
        var unlicensed = items.Single(i => i.GetProperty("userId").GetInt32() == withoutLicence);

        licensed.GetProperty("legalName").GetString().Should().Be(EnterpriseName);
        licensed.GetProperty("hasBusinessLicense").GetBoolean().Should().BeTrue();
        unlicensed.GetProperty("hasBusinessLicense").GetBoolean().Should().BeFalse();

        var profile = (await (await _factory.CreateAuthenticatedClient(withLicence, "Owner")
                .GetAsync("/api/v1/me/tax-profile"))
            .Content.ReadFromJsonAsync<Envelope<TaxProfile>>())!.Data;
        profile.LegalName.Should().Be(EnterpriseName);
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
        string? BusinessType, string? TaxCode, string? LegalName, DateTimeOffset? SubmittedAt, DateTimeOffset? VerifiedAt,
        string? ReviewStatus, string? ReviewNote, bool WithholdingApplies,
        decimal VatRate, decimal PersonalIncomeTaxRate, string Explanation);
}

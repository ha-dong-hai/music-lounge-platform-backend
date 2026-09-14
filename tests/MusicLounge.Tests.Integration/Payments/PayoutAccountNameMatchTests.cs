using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-399. Admin xác minh tài khoản nhận tiền của phòng trà (MLACP-395) — trước đây việc đối chiếu tên chủ tài khoản hoàn
/// toàn bằng mắt, và không gì chặn khi tên lệch. Nay tên chủ tài khoản phải khớp, sau khi bỏ dấu và không phân biệt hoa
/// thường, với họ tên được chốt lúc duyệt CCCD/CMND, hoặc với tên doanh nghiệp đã duyệt.
///
/// <para>Họ tên phải được CHỐT lúc duyệt vì FullName sửa được bất cứ lúc nào mà không mất trạng thái đã duyệt. Mỗi bài một
/// chủ phòng trà riêng.</para>
/// </summary>
[Collection("Integration")]
public sealed class PayoutAccountNameMatchTests
{
    private const string CheckedName = "Đặng Văn Đức";
    private const string CompanyName = "CÔNG TY TNHH PHÒNG TRÀ ĐÊM";

    private readonly ApiFactory _factory;

    public PayoutAccountNameMatchTests(ApiFactory factory) => _factory = factory;

    private sealed record Seller(int OwnerId, int LoungeId);

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<Seller> SellerAsync(
        string fullName = CheckedName, KycReviewStatus card = KycReviewStatus.Approved, string? checkedName = CheckedName,
        PayeeBusinessType? businessType = null, bool enterpriseApproved = false, string? legalName = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User
        {
            Email = $"name399-{Guid.NewGuid():N}@test.com", FullName = fullName, Role = UserRole.Owner,
            IsActive = true, EmailVerifiedAt = DateTimeOffset.UtcNow,
            CitizenCardSubmittedAt = DateTimeOffset.UtcNow.AddDays(-1), CitizenCardReviewStatus = card,
            CitizenCardVerifiedName = card == KycReviewStatus.Approved ? checkedName : null,
            BusinessType = businessType, LegalName = legalName,
            TaxProfileSubmittedAt = businessType is null ? null : DateTimeOffset.UtcNow.AddDays(-1),
            TaxProfileReviewStatus = businessType is null ? null : enterpriseApproved ? KycReviewStatus.Approved : KycReviewStatus.Pending,
            TaxProfileVerifiedAt = enterpriseApproved ? DateTimeOffset.UtcNow.AddDays(-1) : null
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Name399 {Guid.NewGuid():N}"[..20], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Lê Lợi", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return new Seller(owner.Id, lounge.Id);
    }

    private async Task<int> AccountAsync(Seller seller, string holder, bool isDefault = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
        var account = new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = seller.LoungeId, BankName = "Vietcombank",
            AccountNumber = pii.Encrypt($"{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}"), AccountHolder = holder,
            IsDefault = isDefault, IsVerified = false
        };
        db.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private Task<HttpResponseMessage> ReviewAccountAsync(int accountId, bool approve, string? note = null)
        => Admin().PostAsJsonAsync($"/api/v1/admin/bank-accounts/{accountId}/review", new { Approve = approve, Note = note });

    private async Task<bool> IsVerifiedAsync(int accountId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<BankAccount>().AsNoTracking().SingleAsync(a => a.Id == accountId)).IsVerified;
    }

    private async Task<User> OwnerAsync(int ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Users.AsNoTracking().SingleAsync(u => u.Id == ownerId);
    }

    private static string FakeUploadedImage()
    {
        var fileName = $"{Guid.NewGuid():N}.png";
        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsDir);
        File.WriteAllBytes(Path.Combine(uploadsDir, fileName), [0x89, 0x50, 0x4E, 0x47]);
        return $"/uploads/{fileName}";
    }

    // ── Họ tên được chốt lúc duyệt CCCD/CMND ─────────────────────────────────

    [Fact]
    public async Task ApprovingACitizenCard_RecordsTheNameAdminCheckedAgainstTheCard()
    {
        var seller = await SellerAsync(card: KycReviewStatus.Pending);

        (await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{seller.OwnerId}/CitizenCard",
                new { Approve = true, Note = (string?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await OwnerAsync(seller.OwnerId)).CitizenCardVerifiedName.Should().Be(CheckedName);
    }

    [Fact]
    public async Task RenamingTheProfileAfterApproval_DoesNotChangeTheCheckedName()
    {
        var seller = await SellerAsync();

        (await _factory.CreateAuthenticatedClient(seller.OwnerId, "Owner").PutAsJsonAsync("/api/v1/me/profile",
                new { FullName = "Trần Thị Bích", Phone = (string?)null, AvatarUrl = (string?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var owner = await OwnerAsync(seller.OwnerId);
        owner.FullName.Should().Be("Trần Thị Bích");
        owner.CitizenCardVerifiedName.Should().Be(CheckedName, "tên đã chốt là tên Admin đã đối chiếu với giấy tờ");
    }

    [Fact]
    public async Task ResubmittingTheCard_ClearsTheCheckedName()
    {
        var seller = await SellerAsync();

        (await _factory.CreateAuthenticatedClient(seller.OwnerId, "Owner").PostAsJsonAsync("/api/v1/me/citizen-card", new
            {
                CitizenCardNumber = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString(),
                FrontImageUrl = FakeUploadedImage(), BackImageUrl = FakeUploadedImage(), DateOfBirth = "1990-01-01"
            }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await OwnerAsync(seller.OwnerId)).CitizenCardVerifiedName.Should().BeNull("giấy tờ mới chưa ai đối chiếu");
    }

    [Fact]
    public async Task RejectingTheCard_ClearsTheCheckedName()
    {
        var seller = await SellerAsync();

        (await Admin().PostAsJsonAsync($"/api/v1/admin/kyc-reviews/{seller.OwnerId}/CitizenCard",
                new { Approve = false, Note = "Ảnh mờ, không đọc được họ tên" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await OwnerAsync(seller.OwnerId)).CitizenCardVerifiedName.Should().BeNull();
    }

    // ── So tên khi xác minh tài khoản ────────────────────────────────────────

    [Fact]
    public async Task AnAccountInTheCheckedName_CanBeVerified_EvenWrittenWithoutAccents()
    {
        var seller = await SellerAsync();
        var accountId = await AccountAsync(seller, "DANG VAN DUC");

        (await ReviewAccountAsync(accountId, approve: true)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await IsVerifiedAsync(accountId)).Should().BeTrue();
    }

    [Fact]
    public async Task AnAccountInSomeoneElsesName_CannotBeVerified()
    {
        var seller = await SellerAsync();
        var accountId = await AccountAsync(seller, "TRAN THI BICH");

        var res = await ReviewAccountAsync(accountId, approve: true);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("TRAN THI BICH").And.Contain(CheckedName);
        (await IsVerifiedAsync(accountId)).Should().BeFalse();
    }

    [Fact]
    public async Task AfterARename_TheAccountStillHasToMatchTheCheckedName()
    {
        // Duyệt CCCD với tên thật, đổi tên hồ sơ, rồi khai tài khoản đứng tên mới — đúng lỗ hổng mà việc chốt tên chặn.
        var seller = await SellerAsync(fullName: "Trần Thị Bích");
        var accountId = await AccountAsync(seller, "TRAN THI BICH");

        (await ReviewAccountAsync(accountId, approve: true)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await IsVerifiedAsync(accountId)).Should().BeFalse();
    }

    [Fact]
    public async Task AnApprovedEnterprise_MustUseTheCompanyName()
    {
        var seller = await SellerAsync(businessType: PayeeBusinessType.Enterprise, enterpriseApproved: true, legalName: CompanyName);
        var personal = await AccountAsync(seller, "DANG VAN DUC");
        var company = await AccountAsync(seller, "CONG TY TNHH PHONG TRA DEM", isDefault: false);

        var refused = await ReviewAccountAsync(personal, approve: true);
        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("tên doanh nghiệp");

        (await ReviewAccountAsync(company, approve: true)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await IsVerifiedAsync(company)).Should().BeTrue();
    }

    [Fact]
    public async Task AnEnterpriseNotYetApproved_IsComparedWithTheCheckedName()
    {
        // Doanh nghiệp chưa được duyệt được xử lý như hộ/cá nhân — cùng quy tắc khấu trừ thuế.
        var seller = await SellerAsync(businessType: PayeeBusinessType.Enterprise, enterpriseApproved: false, legalName: CompanyName);
        var accountId = await AccountAsync(seller, "DANG VAN DUC");

        (await ReviewAccountAsync(accountId, approve: true)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ACardApprovedBeforeTheNameWasRecorded_MustBeReApprovedFirst()
    {
        var seller = await SellerAsync(checkedName: null);
        var accountId = await AccountAsync(seller, "DANG VAN DUC");

        var res = await ReviewAccountAsync(accountId, approve: true);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("duyệt lại CCCD");
        (await IsVerifiedAsync(accountId)).Should().BeFalse();
    }

    [Fact]
    public async Task AnEnterpriseApprovedBeforeItsNameWasRecorded_MustRedeclare()
    {
        var seller = await SellerAsync(businessType: PayeeBusinessType.Enterprise, enterpriseApproved: true, legalName: null);
        var accountId = await AccountAsync(seller, "CONG TY TNHH PHONG TRA DEM");

        var res = await ReviewAccountAsync(accountId, approve: true);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("khai lại hồ sơ thuế");
    }

    [Fact]
    public async Task RejectingAnAccountInSomeoneElsesName_StillWorks()
    {
        var seller = await SellerAsync();
        var accountId = await AccountAsync(seller, "TRAN THI BICH");

        (await ReviewAccountAsync(accountId, approve: false, note: "Tên chủ tài khoản không khớp CCCD"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

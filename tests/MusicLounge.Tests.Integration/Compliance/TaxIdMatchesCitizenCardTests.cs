using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-660. Từ 01/7/2025 số định danh cá nhân (số CCCD 12 chữ số) là mã số thuế của cá nhân và hộ kinh doanh
/// (TT 86/2024/TT-BTC). Người đã nộp CCCD thì số khai ở hồ sơ thuế phải trùng — khác là gõ nhầm, hoặc khai số của người
/// khác để tiền thuế khấu trừ ghi vào tên họ. Mỗi bài một tài khoản riêng.
/// </summary>
[Collection("Integration")]
public sealed class TaxIdMatchesCitizenCardTests
{
    private readonly ApiFactory _factory;

    public TaxIdMatchesCitizenCardTests(ApiFactory factory) => _factory = factory;

    private async Task<(Guid UserId, string CardNumber)> OwnerWithCitizenCardAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
        var card = Random.Shared.NextInt64(100_000_000_000L, 999_999_999_999L).ToString();
        var user = new User
        {
            Email = $"tax660-{Guid.NewGuid():N}@test.com", FullName = "Chủ hộ kinh doanh 660", Role = UserRole.Owner,
            AuthProvider = "local", EmailVerifiedAt = DateTimeOffset.UtcNow, IsActive = true,
            CitizenCardNumber = pii.Encrypt(card), CitizenCardSubmittedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CitizenCardReviewStatus = KycReviewStatus.Approved
        };
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, card);
    }

    private Task<HttpResponseMessage> DeclareHouseholdAsync(Guid userId, string taxCode)
        => _factory.CreateAuthenticatedClient(userId, "Owner").PutAsJsonAsync("/api/v1/me/tax-profile",
            new { BusinessType = "HouseholdOrIndividual", TaxCode = taxCode });

    [Fact]
    public async Task AHouseholdDeclaringItsOwnCitizenCardNumber_IsAccepted()
    {
        var (userId, card) = await OwnerWithCitizenCardAsync();

        var res = await DeclareHouseholdAsync(userId, card);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AHouseholdDeclaringSomeoneElsesNumber_IsRefused()
    {
        var (userId, card) = await OwnerWithCitizenCardAsync();
        var other = card[..11] + (card[11] == '9' ? '0' : (char)(card[11] + 1));

        var res = await DeclareHouseholdAsync(userId, other);

        ((int)res.StatusCode).Should().Be(422, "a 12-digit number that is not this person's ID is not their tax ID");
        (await res.Content.ReadAsStringAsync()).Should().Contain("trùng số CCCD");
    }
}

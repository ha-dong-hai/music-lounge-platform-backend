using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// Governance gap #8 from the 2026-08-09 production-hardening audit — BankAccount had zero CRUD
/// anywhere even though Settlement.BankAccountId/Donation.BankAccountId depend on it as the real
/// payout destination.
/// POST/GET /api/v1/bank-accounts | PUT /api/v1/bank-accounts/{id}
/// </summary>
[Collection("Integration")]
public sealed class BankAccountTests
{
    private readonly ApiFactory _factory;

    public BankAccountTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_AsLoungeOwner_Returns201()
    {
        var (ownerId, loungeId) = await OwnVenueAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/bank-accounts", new
        {
            OwnerType = "Lounge",
            OwnerId = loungeId,
            BankName = "Vietcombank",
            AccountNumber = "0123456789",
            AccountHolder = "NGUYEN VAN A",
            IsDefault = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Regression test for MLACP-253 (audit-flagged D1 gap, 2026-09-04): BankAccount used to inherit
    /// bare BaseEntity, so there was no record of who added a payout-destination account. Now
    /// inherits AuditableEntity — this confirms the generic ApplicationDbContext.SaveChangesAsync
    /// stamp actually fires for this entity, not just that the property exists.
    /// </summary>
    [Fact]
    public async Task Create_StampsCreatedByWithCurrentUser()
    {
        var (ownerId, loungeId) = await OwnVenueAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/bank-accounts", new
        {
            OwnerType = "Lounge",
            OwnerId = loungeId,
            BankName = "Vietcombank",
            AccountNumber = "0123456789",
            AccountHolder = "NGUYEN VAN A",
            IsDefault = true
        });
        var id = (await res.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = await db.Set<MusicLounge.Domain.Entities.BankAccount>().FindAsync(id);

        account!.CreatedBy.Should().Be(ownerId);
        account.CreatedAt.Should().NotBe(default);
        account.UpdatedAt.Should().BeNull("not yet updated since creation");
    }

    /// <summary>Same regression, Update side — confirms UpdatedBy/UpdatedAt actually populate.</summary>
    [Fact]
    public async Task Update_StampsUpdatedByAndUpdatedAt()
    {
        var (ownerId, loungeId) = await OwnVenueAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var createRes = await client.PostAsJsonAsync("/api/v1/bank-accounts", new
        {
            OwnerType = "Lounge",
            OwnerId = loungeId,
            BankName = "Vietcombank",
            AccountNumber = "0123456789",
            AccountHolder = "NGUYEN VAN A",
            IsDefault = true
        });
        var id = (await createRes.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        await client.PutAsJsonAsync($"/api/v1/bank-accounts/{id}", new
        {
            BankName = "Techcombank",
            AccountNumber = "9876543210",
            AccountHolder = "NGUYEN VAN A",
            IsDefault = true
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = await db.Set<MusicLounge.Domain.Entities.BankAccount>().FindAsync(id);

        account!.UpdatedBy.Should().Be(ownerId);
        account.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_AsNonOwnerOfLounge_Returns403()
    {
        // OtherOwnerId does not own SeedHelper.LoungeId
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/bank-accounts", new
        {
            OwnerType = "Lounge",
            OwnerId = SeedHelper.LoungeId,
            BankName = "Vietcombank",
            AccountNumber = "0123456789",
            AccountHolder = "NGUYEN VAN A",
            IsDefault = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateSecondDefault_ClearsPreviousDefault()
    {
        var (ownerId, loungeId) = await OwnVenueAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        await client.PostAsJsonAsync("/api/v1/bank-accounts", new
        {
            OwnerType = "Lounge",
            OwnerId = loungeId,
            BankName = "Vietcombank",
            AccountNumber = "0111111111",
            AccountHolder = "NGUYEN VAN A",
            IsDefault = true
        });
        await client.PostAsJsonAsync("/api/v1/bank-accounts", new
        {
            OwnerType = "Lounge",
            OwnerId = loungeId,
            BankName = "Techcombank",
            AccountNumber = "0222222222",
            AccountHolder = "NGUYEN VAN A",
            IsDefault = true
        });

        var listRes = await client.GetAsync(
            $"/api/v1/bank-accounts?ownerType=Lounge&ownerId={loungeId}");
        var body = await listRes.Content.ReadAsStringAsync();

        // Exactly one account should still be flagged default after the second Create.
        System.Text.RegularExpressions.Regex.Matches(body, "\"isDefault\":true").Count.Should().Be(1);
    }

    [Fact]
    public async Task GetAll_AsNonOwner_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.GetAsync($"/api/v1/bank-accounts?ownerType=Lounge&ownerId={SeedHelper.LoungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// MLACP-395: phòng trà riêng cho mỗi bài tạo/sửa tài khoản mặc định. Làm việc đó trên phòng trà mẫu sẽ hạ tài khoản đã
    /// xác minh của nó xuống không mặc định — và mọi khoản giải ngân lên lịch sau đó của chủ phòng trà mẫu (nhiều test
    /// khác chờ đợi) bị giữ vì tài khoản nhận tiền chưa xác minh.
    /// </summary>
    private async Task<(int OwnerId, int LoungeId)> OwnVenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new MusicLounge.Domain.Entities.User
        {
            Email = $"bank-owner-{Guid.NewGuid():N}@test.com", FullName = "Bank Owner",
            Role = MusicLounge.Domain.Enums.UserRole.Owner
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLounge.Domain.Entities.MusicLounge
        {
            OwnerId = owner.Id, Name = $"BankVenue-{Guid.NewGuid():N}"[..30],
            Status = MusicLounge.Domain.Enums.LoungeStatus.Approved,
            Address = new MusicLounge.Domain.ValueObjects.VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        return (owner.Id, lounge.Id);
    }

    private sealed record IdResponse(bool Success, int Data);
}

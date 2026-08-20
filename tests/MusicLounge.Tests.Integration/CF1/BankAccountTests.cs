using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/bank-accounts", new
        {
            OwnerType = "Lounge",
            OwnerId = SeedHelper.LoungeId,
            BankName = "Vietcombank",
            AccountNumber = "0123456789",
            AccountHolder = "NGUYEN VAN A",
            IsDefault = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
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
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        await client.PostAsJsonAsync("/api/v1/bank-accounts", new
        {
            OwnerType = "Lounge",
            OwnerId = SeedHelper.LoungeId,
            BankName = "Vietcombank",
            AccountNumber = "0111111111",
            AccountHolder = "NGUYEN VAN A",
            IsDefault = true
        });
        await client.PostAsJsonAsync("/api/v1/bank-accounts", new
        {
            OwnerType = "Lounge",
            OwnerId = SeedHelper.LoungeId,
            BankName = "Techcombank",
            AccountNumber = "0222222222",
            AccountHolder = "NGUYEN VAN A",
            IsDefault = true
        });

        var listRes = await client.GetAsync(
            $"/api/v1/bank-accounts?ownerType=Lounge&ownerId={SeedHelper.LoungeId}");
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
}

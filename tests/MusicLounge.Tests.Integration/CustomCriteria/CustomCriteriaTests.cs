using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using CustomCriteriaEntity = MusicLounge.Domain.Entities.CustomCriteria;

namespace MusicLounge.Tests.Integration.CustomCriteria;

/// <summary>
/// MLACP-266 — first coverage for this module (never audited/tested before this batch).
/// POST/GET /api/v1/custom-criteria | POST /api/v1/custom-criteria/shows/{id}/values
/// </summary>
[Collection("Integration")]
public sealed class CustomCriteriaTests
{
    private readonly ApiFactory _factory;

    public CustomCriteriaTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateCustomCriteria_AsOwner_Returns201()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/custom-criteria", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"Tieu chi {Guid.NewGuid():N}",
            Key = $"key_{Guid.NewGuid():N}"[..20],
            DataType = "Text",
            Options = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>MLACP-266: same B1 authorization-gap class as MLACP-252/256/263 — hand-rolled
    /// OwnerId-only check with no Admin fallback despite RequireOwner policy allowing Admin.</summary>
    [Fact]
    public async Task CreateCustomCriteria_ByAdmin_NotTheOwner_Returns201()
    {
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PostAsJsonAsync("/api/v1/custom-criteria", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"Tieu chi Admin {Guid.NewGuid():N}",
            Key = $"key_{Guid.NewGuid():N}"[..20],
            DataType = "Text",
            Options = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created,
            "Admin must be able to manage any venue's custom criteria, matching the controller's declared RequireOwner policy");
    }

    [Fact]
    public async Task CreateCustomCriteria_DuplicateKey_Returns409()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var key = $"dup_{Guid.NewGuid():N}"[..20];

        var first = await client.PostAsJsonAsync("/api/v1/custom-criteria", new
        {
            LoungeId = SeedHelper.LoungeId, Name = "Tieu chi 1", Key = key, DataType = "Text", Options = (string?)null
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/v1/custom-criteria", new
        {
            LoungeId = SeedHelper.LoungeId, Name = "Tieu chi 2", Key = key, DataType = "Text", Options = (string?)null
        });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCustomCriteria_ByNonOwner_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/custom-criteria", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = "Tieu chi",
            Key = $"key_{Guid.NewGuid():N}"[..20],
            DataType = "Text",
            Options = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>MLACP-266: CustomCriteria was BaseEntity-only — converted to AuditableEntity.</summary>
    [Fact]
    public async Task CreateCustomCriteria_StampsCreatedByWithOwner()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/custom-criteria", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"Tieu chi {Guid.NewGuid():N}",
            Key = $"key_{Guid.NewGuid():N}"[..20],
            DataType = "Text",
            Options = (string?)null
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await res.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var criteria = await db.Set<CustomCriteriaEntity>().FindAsync(id);
        criteria!.CreatedBy.Should().Be(SeedHelper.OwnerId);
    }

    private sealed record DataResponse<T>(bool Success, T Data);
}

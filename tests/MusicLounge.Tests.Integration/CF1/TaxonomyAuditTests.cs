using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// Regression test for MLACP-254 (audit-flagged D1 gap, 2026-09-04): EventCategory/Mood/
/// MusicGenre/VenueAtmosphere used to inherit bare BaseEntity, so there was no record of which
/// Admin created/edited a taxonomy value. Now inherit AuditableEntity — this confirms the generic
/// ApplicationDbContext.SaveChangesAsync stamp actually fires, not just that the property exists.
/// One representative entity (EventCategory) is exercised here since all four share the identical
/// handler pattern (confirmed during the audit); the fix and mechanism are the same for all four.
/// </summary>
[Collection("Integration")]
public sealed class TaxonomyAuditTests
{
    private readonly ApiFactory _factory;

    public TaxonomyAuditTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateEventCategory_StampsCreatedByWithAdmin()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.PostAsJsonAsync("/api/v1/admin/event-categories", new
        {
            Name = $"Genre-{Guid.NewGuid():N}"[..20],
            Description = "Test category"
        });
        var id = (await res.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = await db.Set<EventCategory>().FindAsync(id);

        category!.CreatedBy.Should().Be(SeedHelper.AdminId);
        category.CreatedAt.Should().NotBe(default);
    }

    private sealed record IdResponse(bool Success, int Data);
}

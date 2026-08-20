using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Moderations;

/// <summary>
/// ScoreModerationWithAiJob populates EventModeration.AiScore/RiskLevel/FlagReason/AiRecommendation
/// via Gemini — fail-open by design (GetPendingModerations already orders the Admin queue by
/// AiScore descending, so an unscored row just sits at normal priority, still covered by
/// SlaDeadline). No Gemini:ApiKey is configured in appsettings.Testing.json, so this test exercises
/// exactly the "vendor not configured" path every environment without a key hits.
/// </summary>
[Collection("Integration")]
public sealed class ModerationAiScoringTests
{
    private readonly ApiFactory _factory;

    public ModerationAiScoringTests(ApiFactory factory) => _factory = factory;

    private async Task<int> CreateAndSubmitShowAsync()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"AiScoringTestShow-{Guid.NewGuid():N}",
            Description = "Integration test show for AI moderation scoring",
            Format = "Offline",
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(14),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = 100,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            Performances = Array.Empty<object>()
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();
        var showId = body!.Data;

        await client.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/legal-approval",
            new { LegalApprovalReference = "SoVHTT-AI-TEST-0001" });

        await client.PostAsJsonAsync("/api/v1/ticket-tiers", new
        {
            ShowId = showId,
            Name = "Standard",
            Description = (string?)null,
            AccessType = "Physical",
            ZoneId = (int?)null,
            TotalCapacity = 50,
            Prices = new[]
            {
                new
                {
                    Name = "Standard", Price = 100_000m, Quota = (int?)50, PurchaseChannel = "Both",
                    SaleStart = DateTimeOffset.UtcNow, SaleEnd = DateTimeOffset.UtcNow.AddDays(2)
                }
            }
        });

        var publishRes = await client.PostAsync($"/api/v1/lounge-shows/{showId}/publish", null);
        publishRes.EnsureSuccessStatusCode();

        return showId;
    }

    [Fact]
    public async Task ScoreModerationWithAiJob_WithNoApiKeyConfigured_LeavesModerationUnscoredWithoutThrowing()
    {
        var showId = await CreateAndSubmitShowAsync();

        int moderationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var moderation = await db.EventModerations.FirstAsync(m => m.TargetId == showId);
            moderationId = moderation.Id;
            moderation.AiScore.Should().BeNull("no Gemini:ApiKey is configured in appsettings.Testing.json");
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<ScoreModerationWithAiJob>();
            var act = () => job.ExecuteAsync(moderationId, new JobCancellationToken(false));
            await act.Should().NotThrowAsync("a missing/unavailable AI vendor must fail open, never break moderation");
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloaded = await verifyDb.EventModerations.FirstAsync(m => m.Id == moderationId);
        reloaded.AiScore.Should().BeNull();
        reloaded.RiskLevel.Should().BeNull();
        reloaded.AiRecommendation.Should().BeNull();
    }

    private sealed record IdResponse(bool Success, int Data);
}

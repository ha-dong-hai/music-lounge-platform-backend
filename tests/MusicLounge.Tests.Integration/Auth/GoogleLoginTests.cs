using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Auth;

/// <summary>
/// POST /api/v1/auth/google — master-backend-techlead sweep, found by comparing this handler
/// line-by-line against the structurally identical LoginCommandHandler/VerifyEmailCommandHandler.
/// </summary>
[Collection("Integration")]
public sealed class GoogleLoginTests
{
    private readonly ApiFactory _factory;

    public GoogleLoginTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Google_StaffUser_ReturnsLoungeIdInResponseBody()
    {
        var email = $"staff-google-{Guid.NewGuid():N}@test.com";
        int userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new User
            {
                Email = email, FullName = "Staff Google", Role = UserRole.Staff,
                AuthProvider = "local", IsActive = true, EmailVerifiedAt = DateTimeOffset.UtcNow
            };
            db.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;

            db.Add(new LoungeStaff
            {
                LoungeId = SeedHelper.LoungeId, UserId = userId, AssignedBy = SeedHelper.OwnerId,
                IsActive = true, AssignedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/google",
            new { IdToken = $"google-fake-{userId}|{email}|Staff Google" });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Data.LoungeId.Should().Be(SeedHelper.LoungeId,
            "the handler computes loungeId for Staff users and must thread it into the response " +
            "the same way LoginCommandHandler/VerifyEmailCommandHandler do — the frontend reads " +
            "this exact field (AuthContext.jsx) to scope Staff to their venue");
    }

    private sealed record AuthResponse(bool Success, AuthResultData Data);

    private sealed record AuthResultData(
        string Token, DateTimeOffset ExpiresAt, int UserId, string Email, string FullName,
        string Role, int? LoungeId);
}

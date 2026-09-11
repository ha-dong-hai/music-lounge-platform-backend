using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-367. Trạng thái của phòng trà theo án phạt được tự tính ở sáu chỗ, mỗi chỗ một kiểu:
/// <list type="bullet">
/// <item>ra lệnh cảnh cáo (Admin, hoặc qua khiếu nại) đặt thẳng Warned — kể cả khi phòng trà đang bị khoá
/// hay chưa được duyệt, mà Warned vẫn được hoạt động: một lời cảnh cáo mở khoá phòng trà;</item>
/// <item>lệnh tạm khoá có hiệu lực sau lệnh khoá vĩnh viễn hạ Locked xuống Suspended;</item>
/// <item>chấp thuận kháng cáo luôn báo "venue trở lại hoạt động bình thường" kể cả khi phòng trà vẫn đang
/// chịu án khác; chỉ đếm tạm khoá/khoá vĩnh viễn nên gỡ một cảnh cáo khi còn cảnh cáo khác vẫn về
/// Approved; và ghi đè cả trạng thái không do án phạt nào đặt ra.</item>
/// </list>
/// Nay mọi chỗ dùng chung một quy tắc: áp án thì chỉ nặng thêm, gỡ án thì suy từ các án còn lại và chỉ nhẹ
/// đi — và thông báo nói đúng trạng thái sau cùng.
/// </summary>
[Collection("Integration")]
public sealed class VenueStatusFromPenaltiesTests
{
    private readonly ApiFactory _factory;

    public VenueStatusFromPenaltiesTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<(int OwnerId, int LoungeId)> SeedVenueAsync(LoungeStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User
        {
            Email = $"status-owner-{Guid.NewGuid():N}@test.com", FullName = "Status Owner",
            Role = UserRole.Owner, AuthProvider = "local", EmailVerifiedAt = DateTimeOffset.UtcNow, IsActive = true
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"StatusVenue-{Guid.NewGuid():N}"[..30], Status = status,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        return (owner.Id, lounge.Id);
    }

    /// <param name="applied">Tạm khoá/khoá vĩnh viễn đã qua thời gian báo trước và đã được áp.</param>
    private async Task<int> SeedPenaltyAsync(
        int loungeId, PenaltyType type, PenaltyStatus status = PenaltyStatus.Active, bool applied = true,
        DateTimeOffset? appealDeadline = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTimeOffset.UtcNow;
        var penalty = new VenuePenalty
        {
            LoungeId = loungeId, PenaltyType = type, Reason = "Vi phạm thử nghiệm",
            IssuedBy = SeedHelper.AdminId, IssuedAt = now.AddDays(-2),
            EffectiveAt = applied ? now.AddDays(-1) : now.AddMinutes(-1),
            AppliedAt = applied && type != PenaltyType.Warning ? now.AddDays(-1) : null,
            SuspensionDays = type == PenaltyType.Suspension ? 30 : null,
            SuspensionEnd = applied && type == PenaltyType.Suspension ? now.AddDays(29) : null,
            Status = status, AppealedAt = status == PenaltyStatus.Appealed ? now.AddHours(-1) : null,
            AppealDeadline = appealDeadline
        };
        db.VenuePenalties.Add(penalty);
        await db.SaveChangesAsync();
        return penalty.Id;
    }

    private async Task<LoungeStatus> StatusOfAsync(int loungeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Lounges.SingleAsync(l => l.Id == loungeId)).Status;
    }

    private async Task<string> LatestAppealNoticeToAsync(int ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Notifications
                .Where(n => n.UserId == ownerId && n.Type == NotificationType.AppealResolved)
                .OrderByDescending(n => n.Id)
                .FirstAsync())
            .Body;
    }

    private Task<HttpResponseMessage> IssueWarningAsync(int loungeId)
        => Admin.PostAsJsonAsync("/api/v1/venue-penalties", new
        {
            LoungeId = loungeId, PenaltyType = "Warning", Reason = "Cảnh cáo thử nghiệm",
            EvidenceRef = (string?)null, SuspensionDays = (int?)null
        });

    private Task<HttpResponseMessage> OverturnAsync(int penaltyId)
        => Admin.PostAsJsonAsync($"/api/v1/venue-penalties/{penaltyId}/appeal/review",
            new { Decision = "Overturned", ReviewNote = "Xác minh lại" });

    // ─── Áp án phạt: chỉ nặng thêm ────────────────────────────────────────────

    [Fact]
    public async Task AWarning_OnASuspendedVenue_DoesNotReopenIt()
    {
        var (_, loungeId) = await SeedVenueAsync(LoungeStatus.Suspended);
        await SeedPenaltyAsync(loungeId, PenaltyType.Suspension);

        (await IssueWarningAsync(loungeId)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await StatusOfAsync(loungeId)).Should().Be(LoungeStatus.Suspended,
            "Warned is an operating status — a warning must not lift a suspension");
    }

    [Fact]
    public async Task AWarning_OnAVenueStillAwaitingApproval_DoesNotLetItOperate()
    {
        var (_, loungeId) = await SeedVenueAsync(LoungeStatus.Pending);

        (await IssueWarningAsync(loungeId)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await StatusOfAsync(loungeId)).Should().Be(LoungeStatus.Pending,
            "nobody has approved this venue yet — a warning is not an approval");
    }

    [Fact]
    public async Task AComplaintWarning_OnALockedVenue_DoesNotReopenIt()
    {
        var (_, loungeId) = await SeedVenueAsync(LoungeStatus.Locked);
        await SeedPenaltyAsync(loungeId, PenaltyType.Ban);
        int complaintId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var complaint = new Complaint
            {
                ComplainantUserId = SeedHelper.AudienceId, TargetType = "venue", TargetId = loungeId,
                Category = ComplaintCategory.EventMisrepresentation, Description = "Phòng trà phục vụ kém",
                Status = ComplaintStatus.Open, CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
            };
            db.Set<Complaint>().Add(complaint);
            await db.SaveChangesAsync();
            complaintId = complaint.Id;
        }

        (await Admin.PostAsJsonAsync($"/api/v1/complaints/{complaintId}/resolve",
                new { Status = "Resolved", ResolvedAction = "IssueWarning", Resolution = "Cảnh cáo phòng trà" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await StatusOfAsync(loungeId)).Should().Be(LoungeStatus.Locked);
    }

    [Fact]
    public async Task ASuspensionTakingEffectAfterABan_DoesNotDowngradeTheLock()
    {
        var (_, loungeId) = await SeedVenueAsync(LoungeStatus.Locked);
        await SeedPenaltyAsync(loungeId, PenaltyType.Ban);
        await SeedPenaltyAsync(loungeId, PenaltyType.Suspension, applied: false); // due now, not yet applied

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplyDuePenaltiesJob>()
                .ExecuteAsync(new JobCancellationToken(false));

        (await StatusOfAsync(loungeId)).Should().Be(LoungeStatus.Locked,
            "a lighter penalty must not undo a heavier one — Suspended would let the lock lapse with it");
    }

    // ─── Gỡ án phạt: suy từ các án còn lại, chỉ nhẹ đi, báo đúng ─────────────

    [Fact]
    public async Task AnOverturnedAppeal_WhileAnotherSuspensionStillRuns_KeepsTheVenueSuspended_AndSaysSo()
    {
        var (ownerId, loungeId) = await SeedVenueAsync(LoungeStatus.Suspended);
        var appealed = await SeedPenaltyAsync(loungeId, PenaltyType.Suspension, PenaltyStatus.Appealed,
            appealDeadline: DateTimeOffset.UtcNow.AddHours(40));
        await SeedPenaltyAsync(loungeId, PenaltyType.Suspension);

        (await OverturnAsync(appealed)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await StatusOfAsync(loungeId)).Should().Be(LoungeStatus.Suspended);
        var notice = await LatestAppealNoticeToAsync(ownerId);
        notice.Should().Contain("vẫn đang bị tạm khoá");
        notice.Should().NotContain("hoạt động bình thường",
            "telling an owner their venue is back to normal while it is still suspended is false");
    }

    [Fact]
    public async Task AnAutoApprovedAppeal_WhileAnotherSuspensionStillRuns_SaysSo()
    {
        var (ownerId, loungeId) = await SeedVenueAsync(LoungeStatus.Suspended);
        await SeedPenaltyAsync(loungeId, PenaltyType.Suspension, PenaltyStatus.Appealed,
            appealDeadline: DateTimeOffset.UtcNow.AddMinutes(-1));
        await SeedPenaltyAsync(loungeId, PenaltyType.Suspension);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AutoApproveOverdueAppealsJob>()
                .ExecuteAsync(new JobCancellationToken(false));

        (await StatusOfAsync(loungeId)).Should().Be(LoungeStatus.Suspended);
        var notice = await LatestAppealNoticeToAsync(ownerId);
        notice.Should().Contain("vẫn đang bị tạm khoá");
        notice.Should().NotContain("hoạt động bình thường");
    }

    [Fact]
    public async Task LiftingOneWarning_WhileAnotherStands_LeavesTheVenueWarned()
    {
        var (ownerId, loungeId) = await SeedVenueAsync(LoungeStatus.Warned);
        var appealed = await SeedPenaltyAsync(loungeId, PenaltyType.Warning, PenaltyStatus.Appealed,
            appealDeadline: DateTimeOffset.UtcNow.AddHours(40));
        await SeedPenaltyAsync(loungeId, PenaltyType.Warning);

        (await OverturnAsync(appealed)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await StatusOfAsync(loungeId)).Should().Be(LoungeStatus.Warned, "the other warning still stands");
        (await LatestAppealNoticeToAsync(ownerId)).Should().Contain("vẫn còn cảnh cáo");
    }

    [Fact]
    public async Task AnOverturnedAppeal_DoesNotLiftAStatusThatPenaltyNeverSet()
    {
        // Mirrors SuspensionExpiryTests.AVenueAnAdminHasAlreadyMovedOn_IsNotOverwritten: a suspension can
        // only ever have put the venue at Suspended. Something else put it at Locked, and overturning the
        // suspension is not that something's undoing.
        var (_, loungeId) = await SeedVenueAsync(LoungeStatus.Locked);
        var appealed = await SeedPenaltyAsync(loungeId, PenaltyType.Suspension, PenaltyStatus.Appealed,
            appealDeadline: DateTimeOffset.UtcNow.AddHours(40));

        (await OverturnAsync(appealed)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await StatusOfAsync(loungeId)).Should().Be(LoungeStatus.Locked);
    }
}

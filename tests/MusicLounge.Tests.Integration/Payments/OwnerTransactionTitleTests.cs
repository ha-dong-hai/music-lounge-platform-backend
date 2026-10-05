using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-655. Lịch sử giao dịch của chủ phòng trà (GET /me/transactions) trước đây chỉ có Description nội bộ của sổ cái
/// ("Settlement #01a10b35-… payout") — chủ phòng trà không biết khoản nào của buổi nào. Nay mỗi dòng có Title tiếng Việt
/// nêu loại tiền, đợt, tên buổi diễn và số vé, không chứa mã GUID.
/// </summary>
[Collection("Integration")]
public sealed class OwnerTransactionTitleTests
{
    private static readonly Regex Guidish = new("[0-9a-f]{8}-[0-9a-f]{4}-", RegexOptions.IgnoreCase);
    private readonly ApiFactory _factory;

    public OwnerTransactionTitleTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ATicketPayout_IsTitledWithTheShowTrancheAndTicketCount()
    {
        Guid ownerId;
        string showName = $"Đêm nhạc 655 {Guid.NewGuid():N}"[..20];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
            var owner = new User
            {
                Email = $"owner655-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà 655", Role = UserRole.Owner,
                CitizenCardSubmittedAt = DateTimeOffset.UtcNow.AddDays(-2), CitizenCardReviewStatus = KycReviewStatus.Approved,
                CitizenCardVerifiedName = "Chủ phòng trà 655"
            };
            db.Users.Add(owner);
            await db.SaveChangesAsync();
            ownerId = owner.Id;
            var lounge = new MusicLoungeVenue
            {
                OwnerId = owner.Id, Name = $"Venue655-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
                Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
            };
            db.Lounges.Add(lounge);
            db.Add(new BankAccount
            {
                OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id, BankName = "Test Bank",
                AccountNumber = pii.Encrypt("0000000655"), AccountHolder = "Chu phong tra 655", IsDefault = true, IsVerified = true
            });
            var start = DateTimeOffset.UtcNow.AddDays(-20);
            var show = new LoungeShow
            {
                LoungeId = lounge.Id, Name = showName, Status = LoungeShowStatus.Ended,
                ScheduledStart = start, ScheduledEnd = start.AddHours(2), ActualStart = start, ActualEnd = start.AddHours(2),
                CreatedAt = DateTime.UtcNow
            };
            db.Add(show);
            await db.SaveChangesAsync();
            var payment = new Payment
            {
                OrderId = $"MLACP655-{Guid.NewGuid():N}"[..30], PayerId = SeedHelper.AudienceId, GrossAmount = 1_000_000m,
                NetAmount = 880_000m, Status = PaymentStatus.Confirmed, ReferenceType = "TicketHold", ReferenceId = "0",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(payment);
            await db.SaveChangesAsync();
            for (var i = 0; i < 2; i++)
                db.Add(new Ticket
                {
                    Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId, PriceId = SeedHelper.TicketPriceId,
                    TierId = SeedHelper.TicketTierId, ShowId = show.Id, PaymentId = payment.Id, Status = TicketStatus.Confirmed,
                    PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow
                });
            db.Add(new Settlement
            {
                OwnerId = owner.Id, PaymentId = payment.Id, ReleaseType = SettlementReleaseType.Partial70,
                GrossAmount = 1_000_000m, PreRateApplied = 0.70m, PostRateApplied = 0.30m, NetAmount = 616_000m,
                Status = SettlementStatus.Scheduled, ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>().ExecuteAsync(new JobCancellationToken(false));

        var res = await _factory.CreateAuthenticatedClient(ownerId, "Owner").GetAsync("/api/v1/me/transactions");
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue(body);

        var items = JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        items.Should().ContainSingle("test premise: the released tranche is the owner's only ledger credit");
        var row = items[0];
        row.GetProperty("title").GetString().Should().Be($"Tiền vé đợt 70% — {showName} · 2 vé");
        Guidish.IsMatch(row.GetProperty("title").GetString()!).Should().BeFalse("chủ phòng trà không đọc được mã GUID");
        row.GetProperty("description").GetString().Should().StartWith("Settlement #",
            "the ledger's own reconciliation text is kept unchanged alongside the readable title");
    }
}

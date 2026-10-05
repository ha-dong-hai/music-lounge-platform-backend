using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-645. Đo 05/10/2026 trên DB cục bộ: thông báo về tiền in "783,000đ" (dấu ngăn kiểu Mỹ, theo culture máy chủ),
/// gọi người bằng mã GUID ("Chủ phòng trà #01a10809-…", "donate #01a10a64-…"), và một buổi 6 vé bị giữ đợt 30% sinh 6
/// thông báo giống hệt nhau cho mỗi Admin, không ghi tên buổi.
/// </summary>
[Collection("Integration")]
public sealed class MoneyNoticeReadabilityTests
{
    private readonly ApiFactory _factory;

    public MoneyNoticeReadabilityTests(ApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData(783000, "783.000đ")]
    [InlineData(1232000, "1.232.000đ")]
    [InlineData(0, "0đ")]
    [InlineData(17600.4, "17.600đ")]
    public void VietnameseAmounts_UseADotForThousands_AndNoFractions(decimal amount, string expected)
        => VietnamMoney.Format(amount).Should().Be(expected, "VND has no fractional unit and Vietnamese groups with dots");

    [Fact]
    public async Task SeveralTicketPayoutsInOneRun_AreOneNoticeForTheOwner()
    {
        Guid ownerId;
        Guid[] ids;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = new User
            {
                Email = $"rel645-{Guid.NewGuid():N}@test.com", FullName = "Chu 645", Role = UserRole.Owner,
                CitizenCardSubmittedAt = DateTimeOffset.UtcNow.AddDays(-2), CitizenCardReviewStatus = KycReviewStatus.Approved,
                CitizenCardVerifiedName = "Chu 645"
            };
            db.Users.Add(owner);
            await db.SaveChangesAsync();
            var lounge = new MusicLounge.Domain.Entities.MusicLounge
            {
                OwnerId = owner.Id, Name = $"Venue645-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
                Address = new MusicLounge.Domain.ValueObjects.VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
            };
            db.Lounges.Add(lounge);
            var pii = scope.ServiceProvider.GetRequiredService<MusicLounge.Application.Common.Interfaces.IPiiEncryptionService>();
            var account = new BankAccount
            {
                OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id, BankName = "Test Bank",
                AccountNumber = pii.Encrypt("0000000645"), AccountHolder = "Chu 645", IsDefault = true, IsVerified = true
            };
            db.Add(account);
            var start = DateTimeOffset.UtcNow.AddDays(-20);
            var show = new LoungeShow
            {
                LoungeId = lounge.Id, Name = $"Show645-{Guid.NewGuid():N}"[..20], Status = LoungeShowStatus.Ended,
                ScheduledStart = start, ScheduledEnd = start.AddHours(2), ActualStart = start, ActualEnd = start.AddHours(2),
                CreatedAt = DateTime.UtcNow
            };
            db.Add(show);
            await db.SaveChangesAsync();
            var list = new List<Guid>();
            foreach (var (type, net) in new[] { (SettlementReleaseType.Partial70, 431_200m), (SettlementReleaseType.Partial70, 184_800m) })
            {
                var payment = new Payment
                {
                    OrderId = $"REL645-{Guid.NewGuid():N}"[..30], PayerId = SeedHelper.AudienceId, GrossAmount = 700_000m,
                    NetAmount = 616_000m, Status = PaymentStatus.Confirmed, ReferenceType = "TicketHold", ReferenceId = "0",
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.Add(payment);
                await db.SaveChangesAsync();
                db.Add(new Ticket
                {
                    Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId, PriceId = SeedHelper.TicketPriceId, TierId = SeedHelper.TicketTierId,
                    ShowId = show.Id, PaymentId = payment.Id, Status = TicketStatus.Confirmed, PurchaseChannel = PurchaseChannel.Online,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                var s = new Settlement
                {
                    OwnerId = owner.Id, PaymentId = payment.Id, BankAccountId = account.Id, ReleaseType = type, GrossAmount = 700_000m,
                    PreRateApplied = 0.70m, PostRateApplied = 0.30m, NetAmount = net, Status = SettlementStatus.Scheduled,
                    ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedAt = DateTimeOffset.UtcNow
                };
                db.Add(s);
                await db.SaveChangesAsync();
                list.Add(s.Id);
            }
            ownerId = owner.Id;
            ids = [.. list];
        }

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>().ExecuteAsync(new JobCancellationToken(false));

        using var verify = _factory.Services.CreateScope();
        var notices = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.AsNoTracking()
            .Where(n => n.UserId == ownerId && n.Type == NotificationType.SettlementReleased).ToListAsync();
        notices.Should().ContainSingle("one run, one owner — not one notice per ticket (05/10/2026: 37 in one run)")
            .Which.Body.Should().Contain("2 khoản").And.Contain("616.000đ").And.Contain("đợt 70%: 2 khoản");
    }

    [Fact]
    public async Task SeveralHeldTranchesOfOneShow_AreOneNoticePerAdmin_NamingTheShow()
    {
        Guid[] settlementIds;
        string showName = $"Đêm thử 645 {Guid.NewGuid():N}"[..24];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var start = DateTimeOffset.UtcNow.AddDays(-20);
            // Buổi bị đóng mà chưa từng bắt đầu → cả hai khoản bị giữ chờ Admin (ShowCompletion.WasNeverDelivered).
            var show = new LoungeShow
            {
                LoungeId = SeedHelper.LoungeId, Name = showName, Status = LoungeShowStatus.Ended,
                ScheduledStart = start, ScheduledEnd = start.AddHours(2), ActualStart = null, ActualEnd = start.AddHours(8),
                CreatedAt = DateTime.UtcNow
            };
            db.Add(show);
            await db.SaveChangesAsync();
            var accountId = await db.Set<BankAccount>()
                .Where(a => a.OwnerType == BankAccountOwnerType.Lounge && a.OwnerId == SeedHelper.LoungeId)
                .Select(a => a.Id).FirstAsync();
            var ids = new List<Guid>();
            for (var i = 0; i < 2; i++)
            {
                var payment = new Payment
                {
                    OrderId = $"MLACP645-{Guid.NewGuid():N}"[..30], PayerId = SeedHelper.AudienceId, GrossAmount = 700_000m,
                    NetAmount = 616_000m, Status = PaymentStatus.Confirmed, ReferenceType = "TicketHold", ReferenceId = "0",
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.Add(payment);
                await db.SaveChangesAsync();
                db.Add(new Ticket
                {
                    Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId, PriceId = SeedHelper.TicketPriceId,
                    TierId = SeedHelper.TicketTierId, ShowId = show.Id, PaymentId = payment.Id, Status = TicketStatus.Confirmed,
                    PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow
                });
                var s = new Settlement
                {
                    OwnerId = SeedHelper.OwnerId, PaymentId = payment.Id, BankAccountId = accountId,
                    ReleaseType = SettlementReleaseType.Final30, GrossAmount = 700_000m, PreRateApplied = 0.70m,
                    PostRateApplied = 0.30m, NetAmount = 616_000m, Status = SettlementStatus.Scheduled,
                    ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedAt = DateTimeOffset.UtcNow
                };
                db.Add(s);
                await db.SaveChangesAsync();
                ids.Add(s.Id);
            }
            settlementIds = [.. ids];
        }

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>().ExecuteAsync(new JobCancellationToken(false));

        using var verify = _factory.Services.CreateScope();
        var refs = settlementIds.Select(id => id.ToString()).ToList();
        var notices = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.AsNoTracking()
            .Where(n => n.UserId == SeedHelper.AdminId && n.Type == NotificationType.SettlementPendingReview && refs.Contains(n.ReferenceId!))
            .ToListAsync();
        var notice = notices.Should().ContainSingle("two held tranches of ONE show are one thing for the Admin to review").Subject;
        notice.Body.Should().Contain(showName, "the Admin must know which show")
            .And.Contain("2 khoản").And.Contain("1.232.000đ");
    }
}

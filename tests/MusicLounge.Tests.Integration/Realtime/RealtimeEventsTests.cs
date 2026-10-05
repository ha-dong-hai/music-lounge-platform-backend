using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Fakes;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Realtime;

/// <summary>
/// MLACP-669. Admin từ chối tài khoản nhận tiền mà màn của chủ phòng trà vẫn hiện "Chờ Admin duyệt" cho tới khi tải lại
/// trang: web không có kênh nào báo "dữ liệu của bạn vừa đổi". Nay mọi lần lưu được dịch thành sự kiện (thông báo mới cho
/// người nhận, hàng việc chờ cho Admin) và phát SAU KHI commit.
///
/// <para>Test này không mở kết nối SignalR thật (dự án test không có SignalR client); nó khẳng định nội dung và thời điểm
/// phát qua <see cref="RecordingRealtimeNotifier"/>. Phần hub — ghép người nhận vào nhóm — được kiểm trên trình duyệt.</para>
/// </summary>
[Collection("Integration")]
public sealed class RealtimeEventsTests
{
    private readonly ApiFactory _factory;

    public RealtimeEventsTests(ApiFactory factory) => _factory = factory;

    private RecordingRealtimeNotifier Recorder()
    {
        var r = (RecordingRealtimeNotifier)_factory.Services.GetRequiredService<IRealtimeNotifier>();
        r.Events.Clear();
        return r;
    }

    private sealed record Seller(Guid OwnerId, Guid LoungeId, Guid AccountId);

    private async Task<Seller> SellerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
        var owner = new User
        {
            Email = $"rt669-{Guid.NewGuid():N}@test.com", FullName = "Đặng Văn Đức", Role = UserRole.Owner,
            IsActive = true, EmailVerifiedAt = DateTimeOffset.UtcNow,
            CitizenCardSubmittedAt = DateTimeOffset.UtcNow.AddDays(-1), CitizenCardReviewStatus = KycReviewStatus.Approved,
            CitizenCardVerifiedName = "Đặng Văn Đức"
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Rt669 {Guid.NewGuid():N}"[..20], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Lê Lợi", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        var account = new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id, BankName = "NCB",
            AccountNumber = pii.Encrypt("9704198526"), AccountHolder = "NGUYEN VAN A", IsDefault = true
        };
        db.Add(account);
        await db.SaveChangesAsync();
        return new Seller(owner.Id, lounge.Id, account.Id);
    }

    [Fact]
    public async Task RejectingAPayoutAccount_TellsItsOwner_AndEveryAdmin()
    {
        var seller = await SellerAsync();
        var rec = Recorder();

        (await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
                .PostAsJsonAsync($"/api/v1/admin/bank-accounts/{seller.AccountId}/review", new { Approve = false, Note = "không trùng tên" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        rec.Events.Should().Contain(new RealtimeEvent(seller.OwnerId, "notification", "bank_account", seller.AccountId.ToString()),
            "màn tài khoản nhận tiền của chủ phòng trà phải biết để tải lại — không có thì vẫn hiện 'Chờ Admin duyệt'");
        rec.Events.Should().Contain(new RealtimeEvent(null, "bank-accounts"),
            "Admin khác đang mở hàng chờ cũng phải thấy tài khoản rời hàng chờ, và số đếm trên menu giảm");
    }

    [Fact]
    public async Task OwnerAddingAnAccount_TellsAdmins_TheQueueChanged()
    {
        var seller = await SellerAsync();
        var rec = Recorder();

        (await _factory.CreateAuthenticatedClient(seller.OwnerId, "Owner").PostAsJsonAsync("/api/v1/bank-accounts", new
            {
                OwnerType = "Lounge", OwnerId = seller.LoungeId, BankName = "Vietcombank",
                AccountNumber = "0371000482915", AccountHolder = "DANG VAN DUC", IsDefault = false
            }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        rec.Events.Should().Contain(new RealtimeEvent(null, "bank-accounts"));
    }

    [Fact]
    public async Task InsideATransaction_NothingIsSentUntilCommit()
    {
        var seller = await SellerAsync();
        var rec = Recorder();
        using var scope = _factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await uow.BeginTransactionAsync();
        uow.Repository<Notification, Guid>().Add(new Notification
        {
            UserId = seller.OwnerId, Type = NotificationType.KycReviewResult, Title = "t", Body = "b",
            ReferenceType = "bank_account", ReferenceId = seller.AccountId.ToString(), CreatedAt = DateTimeOffset.UtcNow
        });
        await uow.SaveChangesAsync();
        rec.Events.Should().BeEmpty("chưa commit — trình duyệt tải lại lúc này sẽ đọc phải dữ liệu cũ");

        await uow.CommitTransactionAsync();
        rec.Events.Should().ContainSingle(e => e.UserId == seller.OwnerId && e.Topic == "notification");
    }

    [Fact]
    public async Task ARolledBackTransaction_SendsNothing()
    {
        var seller = await SellerAsync();
        var rec = Recorder();
        using var scope = _factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await uow.BeginTransactionAsync();
        uow.Repository<Notification, Guid>().Add(new Notification
        {
            UserId = seller.OwnerId, Type = NotificationType.KycReviewResult, Title = "t", Body = "b", CreatedAt = DateTimeOffset.UtcNow
        });
        await uow.SaveChangesAsync();
        await uow.RollbackTransactionAsync();

        rec.Events.Should().BeEmpty("không có gì được lưu thì không ai cần tải lại");

        // Lần lưu sau trong cùng scope không được mang theo sự kiện của lần đã rollback.
        uow.Repository<BankAccount, Guid>().Update((await uow.Repository<BankAccount, Guid>().GetByIdAsync(seller.AccountId))!);
        await uow.SaveChangesAsync();
        rec.Events.Should().NotContain(e => e.Topic == "notification");
    }

    [Fact]
    public async Task ReviewingACitizenCard_PingsTheKycQueue_AndTellsTheSubmitter()
    {
        var seller = await SellerAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var u = await db.Users.FindAsync(seller.OwnerId);
            u!.CitizenCardReviewStatus = KycReviewStatus.Pending;
            u.CitizenCardVerifiedName = null;
            await db.SaveChangesAsync();
        }
        var rec = Recorder();

        (await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PostAsJsonAsync(
                $"/api/v1/admin/kyc-reviews/{seller.OwnerId}/CitizenCard", new { Approve = false, Note = "ảnh mờ" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        rec.Events.Should().Contain(new RealtimeEvent(null, "kyc-reviews"));
        rec.Events.Should().Contain(e => e.UserId == seller.OwnerId && e.Topic == "notification");
    }

    [Fact]
    public async Task EditingAnOrdinaryProfile_DoesNotPingTheKycQueue()
    {
        var seller = await SellerAsync();
        var rec = Recorder();

        (await _factory.CreateAuthenticatedClient(seller.OwnerId, "Owner")
                .PutAsJsonAsync("/api/v1/me/profile", new { FullName = "Đặng Văn Đức", PhoneNumber = (string?)null }))
            .IsSuccessStatusCode.Should().BeTrue();

        rec.Events.Should().NotContain(e => e.Topic == "kyc-reviews",
            "User bị sửa ở rất nhiều đường; chỉ cột thuộc hàng đợi định danh mới đáng báo Admin");
    }
}

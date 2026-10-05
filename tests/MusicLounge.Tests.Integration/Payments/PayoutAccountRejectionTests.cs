using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-668. Admin bấm "Từ chối" tài khoản nhận tiền của phòng trà mà không gì đổi: lệnh duyệt chỉ ghi
/// <c>IsVerified = false</c> — đúng giá trị tài khoản đang chờ vốn có — nên tài khoản nằm lại hàng chờ của Admin, chủ
/// phòng trà vẫn thấy "Chờ Admin duyệt", và lý do từ chối chỉ còn trong một thông báo.
///
/// <para>Mỗi bài một chủ phòng trà + phòng trà riêng, không sửa dữ liệu seed dùng chung.</para>
/// </summary>
[Collection("Integration")]
public sealed class PayoutAccountRejectionTests
{
    private const string Holder = "Đặng Văn Đức";

    private readonly ApiFactory _factory;

    public PayoutAccountRejectionTests(ApiFactory factory) => _factory = factory;

    private sealed record Seller(Guid OwnerId, Guid LoungeId, Guid AccountId);

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<Seller> SellerWithPendingAccountAsync(string accountHolder)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
        var owner = new User
        {
            Email = $"rej668-{Guid.NewGuid():N}@test.com", FullName = Holder, Role = UserRole.Owner,
            IsActive = true, EmailVerifiedAt = DateTimeOffset.UtcNow,
            CitizenCardSubmittedAt = DateTimeOffset.UtcNow.AddDays(-1), CitizenCardReviewStatus = KycReviewStatus.Approved,
            CitizenCardVerifiedName = Holder
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Rej668 {Guid.NewGuid():N}"[..20], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Lê Lợi", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        var account = new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id, BankName = "NCB",
            AccountNumber = pii.Encrypt($"{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}"),
            AccountHolder = accountHolder, IsDefault = true, IsVerified = false
        };
        db.Add(account);
        await db.SaveChangesAsync();
        return new Seller(owner.Id, lounge.Id, account.Id);
    }

    private Task<HttpResponseMessage> ReviewAsync(Guid accountId, bool approve, string? note = null)
        => Admin().PostAsJsonAsync($"/api/v1/admin/bank-accounts/{accountId}/review", new { Approve = approve, Note = note });

    private static JsonElement Data(string body)
    {
        var root = JsonDocument.Parse(body).RootElement;
        return root.TryGetProperty("data", out var d) ? d : root;
    }

    /// <summary>Dòng của tài khoản trong hàng đợi Admin, hoặc null khi không có. Mỗi phép kiểm "không có" trong file này
    /// đi sau một phép kiểm "có" trên cùng danh sách — nếu không, một danh sách rỗng (hay sai đường dẫn) cũng làm nó xanh.</summary>
    private async Task<JsonElement?> InAdminQueueAsync(Guid accountId, string query = "")
    {
        var res = await Admin().GetAsync($"/api/v1/admin/bank-accounts?pageSize=500{query}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = Data(await res.Content.ReadAsStringAsync()).GetProperty("items");
        foreach (var item in items.EnumerateArray())
            if (item.GetProperty("id").GetGuid() == accountId) return item;
        return null;
    }

    /// <summary>Tài khoản như chính chủ phòng trà thấy ở màn "Tài khoản nhận tiền".</summary>
    private async Task<JsonElement> AsOwnerSeesAsync(Seller seller)
    {
        var res = await _factory.CreateAuthenticatedClient(seller.OwnerId, "Owner")
            .GetAsync($"/api/v1/bank-accounts?ownerType=Lounge&ownerId={seller.LoungeId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return Data(await res.Content.ReadAsStringAsync()).EnumerateArray()
            .Single(a => a.GetProperty("id").GetGuid() == seller.AccountId);
    }

    [Fact]
    public async Task RejectedAccount_LeavesTheAdminQueue_AndShowsUnderRejected()
    {
        var seller = await SellerWithPendingAccountAsync("NGUYEN VAN A");
        (await InAdminQueueAsync(seller.AccountId)).Should().NotBeNull("trước khi từ chối, tài khoản đang chờ duyệt");

        (await ReviewAsync(seller.AccountId, approve: false, note: "không trùng tên")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        (await InAdminQueueAsync(seller.AccountId)).Should().BeNull(
            "đã từ chối thì việc tiếp theo là của chủ phòng trà — để lại trong hàng chờ thì Admin không xoá được nó");
        var rejected = await InAdminQueueAsync(seller.AccountId, "&rejected=true");
        rejected.Should().NotBeNull("Admin vẫn phải xem lại được những tài khoản đã từ chối");
        rejected!.Value.GetProperty("rejectionNote").GetString().Should().Be("không trùng tên");
        rejected.Value.GetProperty("rejectedAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task Owner_SeesTheRejectionAndItsReason()
    {
        var seller = await SellerWithPendingAccountAsync("NGUYEN VAN A");
        (await AsOwnerSeesAsync(seller)).GetProperty("rejectedAt").ValueKind.Should().Be(JsonValueKind.Null);

        await ReviewAsync(seller.AccountId, approve: false, note: "  không trùng tên  ");

        var account = await AsOwnerSeesAsync(seller);
        account.GetProperty("isVerified").GetBoolean().Should().BeFalse();
        account.GetProperty("rejectedAt").ValueKind.Should().Be(JsonValueKind.String,
            "không có dấu này thì chủ phòng trà chỉ thấy 'Chờ Admin duyệt' và chờ mãi");
        account.GetProperty("rejectionNote").GetString().Should().Be("không trùng tên");

        // Lý do nằm giữa câu: trước đây thiếu dấu ngắt nên thông báo đọc thành "Lý do: không trùng tên Hãy cập nhật…".
        using var scope = _factory.Services.CreateScope();
        var notice = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<Notification>()
            .Single(n => n.UserId == seller.OwnerId && n.ReferenceId == seller.AccountId.ToString());
        notice.Body.Should().Contain("Lý do: “không trùng tên”. Hãy cập nhật");
        notice.BodyEn.Should().Contain("Reason: “không trùng tên”. Please update");
    }

    [Fact]
    public async Task EditingARejectedAccount_SendsItBackToTheQueue()
    {
        var seller = await SellerWithPendingAccountAsync("NGUYEN VAN A");
        await ReviewAsync(seller.AccountId, approve: false, note: "không trùng tên");

        (await _factory.CreateAuthenticatedClient(seller.OwnerId, "Owner").PutAsJsonAsync(
                $"/api/v1/bank-accounts/{seller.AccountId}",
                new { BankName = "NCB", AccountNumber = "9704198526", AccountHolder = "DANG VAN DUC", IsDefault = true }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var account = await AsOwnerSeesAsync(seller);
        account.GetProperty("rejectedAt").ValueKind.Should().Be(JsonValueKind.Null, "sửa lại là nộp lại");
        account.GetProperty("rejectionNote").ValueKind.Should().Be(JsonValueKind.Null);
        (await InAdminQueueAsync(seller.AccountId)).Should().NotBeNull("tài khoản đã sửa phải được Admin duyệt lại");
    }

    [Fact]
    public async Task VerifyingAfterARejection_ClearsTheRejection()
    {
        var seller = await SellerWithPendingAccountAsync("DANG VAN DUC");
        await ReviewAsync(seller.AccountId, approve: false, note: "ảnh chụp sao kê mờ");
        (await InAdminQueueAsync(seller.AccountId, "&rejected=true")).Should().NotBeNull();

        (await ReviewAsync(seller.AccountId, approve: true)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var account = await AsOwnerSeesAsync(seller);
        account.GetProperty("isVerified").GetBoolean().Should().BeTrue();
        account.GetProperty("rejectedAt").ValueKind.Should().Be(JsonValueKind.Null,
            "đã xác minh mà còn dấu từ chối thì giao diện hiện hai trạng thái trái nhau");
        (await InAdminQueueAsync(seller.AccountId, "&rejected=true")).Should().BeNull();
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// MLACP-467. Phần hành vi của cùng một lỗi mà <c>EditableFieldsAreReadableTests</c> canh phần hình dạng.
///
/// <para>Phép quét kia chỉ chứng minh DTO có KHAI BÁO trường; nó không chứng minh hàm ánh xạ có ĐIỀN giá
/// trị vào. Thêm trường vào record mà quên sửa hàm ánh xạ thì client vẫn đọc ra <c>null</c>, rồi gửi
/// <c>null</c> ngược lên — mất dữ liệu y như cũ, mà phép quét vẫn xanh. Hai test phải đi cùng nhau.</para>
///
/// <para>Kịch bản đúng như màn hình Sửa của frontend làm: tạo → đọc về → gửi lại nguyên si thứ đọc được →
/// đọc lại. Nếu đường đọc hỏng, vòng này xoá mất giá trị và test đỏ.</para>
/// </summary>
[Collection("Integration")]
public sealed class EditRoundTripTests
{
    private readonly ApiFactory _factory;

    public EditRoundTripTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SuaBuoiHoaNhac_GuiLaiDungThuDocDuoc_KhongMatDanhMucVaHanMucVe()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        // Danh mục có thật để gán: nếu hệ thống chưa có danh mục nào thì test này không kiểm được gì,
        // nên phải dừng với lý do rõ ràng thay vì xanh trong im lặng.
        var cats = await client.GetFromJsonAsync<JsonElement>("/api/v1/catalog/event-categories");
        var catalogItems = cats.TryGetProperty("data", out var d) ? d : cats;
        catalogItems.GetArrayLength().Should().BeGreaterThan(0,
            "không có danh mục nào trong hệ thống thì không kiểm được việc sửa có giữ danh mục hay không");
        var categoryId = catalogItems[0].GetProperty("id").GetInt32();

        var create = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"RoundTrip-{Guid.NewGuid():N}",
            Description = "Buổi hòa nhạc dùng để kiểm vòng đọc–ghi",
            Format = "Offline",
            ScheduledStart = SeedHelper.NextShowStart(),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = categoryId,
            OfflineQuota = 120,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            Performances = new[]
            {
                new { PerformerId = (int?)null, PerformerName = "DJ RoundTrip", Role = "Main", OrderIndex = 1, SetTime = (string?)null, AcceptsDonation = true }
            }
        });
        create.EnsureSuccessStatusCode();
        var showId = (await create.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;

        // 1. Đọc về — đây là tất cả những gì màn hình Sửa có trong tay.
        var docLan1 = await DocChiTiet(client, showId);
        docLan1.GetProperty("categoryId").GetInt32().Should().Be(categoryId,
            "hàm ánh xạ phải điền categoryId; khai báo trong DTO thôi chưa đủ");
        docLan1.GetProperty("offlineQuota").GetInt32().Should().Be(120);

        // 2. Gửi lại đúng thứ đọc được, không đụng vào danh mục và hạn mức — giống người dùng chỉ sửa mô tả.
        var update = await client.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}", new
        {
            Name = docLan1.GetProperty("name").GetString(),
            Description = "Đổi mỗi mô tả, không đụng danh mục",
            ScheduledStart = docLan1.GetProperty("scheduledStart").GetDateTimeOffset(),
            ScheduledEnd = (DateTimeOffset?)null,
            Format = docLan1.GetProperty("format").GetString(),
            CategoryId = docLan1.GetProperty("categoryId").GetInt32(),
            OfflineQuota = docLan1.GetProperty("offlineQuota").GetInt32(),
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            Performances = new[]
            {
                new { PerformerId = (int?)null, PerformerName = "DJ RoundTrip", Role = "Main", OrderIndex = 1, SetTime = (string?)null, AcceptsDonation = true }
            }
        });
        update.EnsureSuccessStatusCode();

        // 3. Đọc lại: danh mục và hạn mức phải còn nguyên.
        var docLan2 = await DocChiTiet(client, showId);
        docLan2.GetProperty("categoryId").GetInt32().Should().Be(categoryId,
            "sửa mô tả không được làm mất danh mục — đây chính là lỗi MLACP-467");
        docLan2.GetProperty("offlineQuota").GetInt32().Should().Be(120,
            "sửa mô tả không được làm mất hạn mức vé offline");
        docLan2.GetProperty("description").GetString().Should().Be("Đổi mỗi mô tả, không đụng danh mục");
    }

    [Fact]
    public async Task ChiTietPhongTra_PhaiTraVeAtmosphereId_DeSuaKhongMatKhongKhi()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.GetAsync($"/api/v1/lounges/{SeedHelper.LoungeId}");
        res.EnsureSuccessStatusCode();
        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        var lounge = body.TryGetProperty("data", out var d) ? d : body;

        // Trường phải TỒN TẠI. Giá trị có thể null (phòng trà chưa chọn không khí), nhưng nếu cả trường
        // cũng không có thì màn hình Sửa không có cách nào gửi lại đúng không khí đang chọn.
        lounge.TryGetProperty("atmosphereId", out _).Should().BeTrue(
            "UpdateLoungeCommand nhận AtmosphereId để ghi đè, nên DTO đọc phải trả AtmosphereId; " +
            "AtmosphereName là để hiển thị, không dò ngược ra Id được");
    }

    // Mỗi lớp test trong dự án này tự khai kiểu bọc phản hồi của riêng nó (xem EventManagementTests,
    // FnbOnlinePaymentTests…) — giữ đúng thói quen đó thay vì thêm một kiểu dùng chung mới.
    private sealed record DataResponse<T>(bool Success, T Data);

    private static async Task<JsonElement> DocChiTiet(HttpClient client, int showId)
    {
        var res = await client.GetAsync($"/api/v1/lounge-shows/{showId}");
        res.EnsureSuccessStatusCode();
        var root = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        return root.TryGetProperty("data", out var d) ? d : root;
    }
}

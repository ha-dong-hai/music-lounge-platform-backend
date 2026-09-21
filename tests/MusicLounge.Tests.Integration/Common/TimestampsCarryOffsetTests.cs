using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Common;

/// <summary>
/// Mốc thời gian trả cho giao diện phải mang MÚI GIỜ.
///
/// <para>Cột thời gian trong cơ sở dữ liệu không lưu múi giờ, nên EF đọc lên được một mốc
/// <c>Kind=Unspecified</c>. Khai kiểu <c>DateTime</c> ở DTO thì mốc đó tuần tự hoá thành chuỗi KHÔNG có
/// phần múi giờ — "2026-08-17T13:12:19.83" — và trình duyệt hiểu chuỗi như vậy là giờ ĐỊA PHƯƠNG. Ở Việt
/// Nam thành lệch đúng 7 tiếng, im lặng, trên mọi màn hình đọc mốc đó.</para>
///
/// <para>Khai <c>DateTimeOffset</c> thì chuyển đổi ngầm gắn múi giờ của máy chủ (App Service Linux chạy
/// UTC, đã kiểm: không đặt WEBSITE_TIME_ZONE) và chuỗi ra có "+00:00".</para>
///
/// <para>TRẦN GIỚI HẠN của cách này: nó đúng vì máy chủ chạy UTC. Đặt múi giờ khác cho App Service thì
/// mọi mốc đọc từ cơ sở dữ liệu sẽ bị gắn nhầm múi giờ đó. Đường nâng cấp thật là ép
/// <c>DateTimeKind.Utc</c> ngay khi đọc lên, nhưng đó là đụng vào cả 139 trường và cả tầng dữ liệu —
/// không làm ở đây. (MLACP-475)</para>
/// </summary>
[Collection("Integration")]
public sealed class TimestampsCarryOffsetTests
{
    private readonly ApiFactory _factory;

    public TimestampsCarryOffsetTests(ApiFactory factory) => _factory = factory;

    /// <summary>Có phần múi giờ ở cuối: "+07:00", "-05:00" hoặc "Z".</summary>
    private static readonly Regex CoMuiGio = new(@"([+-]\d{2}:\d{2}|Z)$", RegexOptions.Compiled);

    [Fact]
    public void KhongDtoNaoDuocKhaiDateTimeTran()
    {
        // Quét MỌI kiểu public của tầng Application, không chỉ những kiểu tên kết thúc bằng "Dto".
        // Bản đầu của phép kiểm này chỉ quét *Dto và vì thế BỎ SÓT ExportedProfile.CreatedAt trong bản
        // xuất dữ liệu cá nhân — cùng một lỗi, chỉ khác cái tên. Lọc theo tên là lọc theo quy ước đặt
        // tên, mà quy ước thì chỗ nào cũng có ngoại lệ. Đã kiểm: không lệnh/truy vấn nào nhận DateTime
        // trần, nên siết rộng ra như vậy không chặn nhầm gì cả.
        var assembly = typeof(UserAdminDto).Assembly;
        var dtos = assembly.GetTypes().Where(t => t.IsPublic).ToList();

        // Chặn "quét trúng số không": nếu cách tìm kiểu hỏng thì danh sách rỗng và phép kiểm xanh vô nghĩa.
        dtos.Should().HaveCountGreaterThan(200, "phải quét được phần lớn kiểu của tầng Application, nếu không thì cách quét đã hỏng");

        var pham = new List<string>();
        var soTruongThoiGian = 0;
        foreach (var t in dtos)
            foreach (var p in t.GetProperties())
            {
                var kieu = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                if (kieu == typeof(DateTimeOffset)) soTruongThoiGian++;
                if (kieu != typeof(DateTime)) continue;
                soTruongThoiGian++;
                pham.Add($"{t.Name}.{p.Name}");
            }

        soTruongThoiGian.Should().BeGreaterThan(50, "phải thấy được các trường thời gian, nếu không thì phép kiểm không kiểm gì");

        pham.Should().BeEmpty(
            "DateTime trần tuần tự hoá KHÔNG kèm múi giờ, nên trình duyệt hiểu là giờ địa phương và hiện "
            + "lệch 7 tiếng ở Việt Nam. Dùng DateTimeOffset như mọi trường thời gian khác. Đang sai: "
            + string.Join(", ", pham));
    }

    [Fact]
    public async Task HangDoiTaiKhoanNhanTien_MocThoiGianCoMuiGio()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
            db.Add(new BankAccount
            {
                OwnerType = BankAccountOwnerType.Lounge,
                OwnerId = SeedHelper.LoungeId,
                BankName = "Ngân hàng Kiểm Thử Múi Giờ",
                AccountNumber = pii.Encrypt("5550001112223"),
                AccountHolder = "Nguyen Van Mui Gio",
                IsDefault = false,
                IsVerified = false
            });
            await db.SaveChangesAsync();
        }

        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await admin.GetAsync("/api/v1/admin/bank-accounts?pageSize=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        var data = root.TryGetProperty("data", out var d) ? d : root;
        var items = data.TryGetProperty("items", out var i) ? i : data;

        var mocs = items.EnumerateArray()
            .Select(x => x.GetProperty("createdAt").GetString())
            .ToList();

        mocs.Should().NotBeEmpty("phải có ít nhất dòng vừa dựng, nếu không phép kiểm không kiểm gì");

        foreach (var moc in mocs)
            CoMuiGio.IsMatch(moc!).Should().BeTrue(
                $"mốc \"{moc}\" không có múi giờ nên trình duyệt ở Việt Nam sẽ hiện lệch 7 tiếng");
    }
}

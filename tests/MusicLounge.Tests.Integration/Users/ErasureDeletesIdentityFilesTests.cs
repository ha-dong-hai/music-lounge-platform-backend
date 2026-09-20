using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Users;

/// <summary>
/// Xoá dữ liệu cá nhân phải xoá CHÍNH TỆP ảnh căn cước, không chỉ xoá đường dẫn trong cơ sở dữ liệu.
///
/// <para>Trước bản sửa này, lớp xử lý xoá dữ liệu gán <c>null</c> cho hai cột đường dẫn nhưng không hề
/// phụ thuộc kho tệp — hai tấm ảnh giấy tờ tuỳ thân ở lại trong <c>App_Data/private-uploads</c> vĩnh viễn.
/// Tệ hơn: sau khi gán null thì không còn đường nào tìm lại tệp đó để dọn tay, vì tham chiếu duy nhất
/// tới nó vừa bị xoá. Luật 91/2025/QH15 Điều 19 buộc xoá chính dữ liệu chứ không phải xoá tham chiếu.</para>
///
/// <para>Test đi qua đúng đường thật: lưu ảnh qua kho tệp, chuyển vào vùng riêng tư đúng như luồng nộp
/// căn cước làm, gắn vào tài khoản, gọi endpoint xoá dữ liệu, rồi hỏi lại kho tệp. Không mô phỏng, không
/// giả lập — nếu dây nối bị tháo thì test đỏ.</para>
/// </summary>
[Collection("Integration")]
public sealed class ErasureDeletesIdentityFilesTests
{
    private readonly ApiFactory _factory;

    public ErasureDeletesIdentityFilesTests(ApiFactory factory) => _factory = factory;

    // PNG 1x1 hợp lệ: kho tệp kiểm nội dung thật chứ không tin phần mở rộng, nên không thể đưa byte bừa.
    private static readonly byte[] AnhPngHopLe = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public async Task XoaDuLieuCaNhan_PhaiXoaLuonHaiTepAnhCanCuoc()
    {
        var email = $"erase-file-{Guid.NewGuid():N}@test.com";
        const string matKhau = "Nh4c!Song#2026$Aa";

        var client = _factory.CreateClient();
        var dangKy = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = matKhau, FullName = "Người nộp căn cước",
            Phone = (string?)null, AcceptTerms = true
        });
        dangKy.IsSuccessStatusCode.Should().BeTrue("phải tạo được tài khoản thì mới kiểm được việc xoá");

        string thamChieuTruoc, thamChieuSau;
        int userId;

        using (var scope = _factory.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Đúng luồng nộp căn cước: upload như ảnh thường rồi chuyển vào vùng riêng tư.
            thamChieuTruoc = await ChuyenVaoVungRiengTuAsync(storage, "cccd-truoc.png");
            thamChieuSau = await ChuyenVaoVungRiengTuAsync(storage, "cccd-sau.png");

            var user = await db.Users.SingleAsync(u => u.Email == email);
            userId = user.Id;
            user.CitizenCardFrontImageUrl = thamChieuTruoc;
            user.CitizenCardBackImageUrl = thamChieuSau;
            user.CitizenCardNumber = "079200000001";
            await db.SaveChangesAsync();
        }

        // Tiền đề của phép kiểm: hai tệp PHẢI đang tồn tại. Nếu không, test sẽ "xanh" chỉ vì
        // chẳng có gì để xoá — đúng kiểu xanh vô nghĩa cần chặn.
        (await MoDuocAsync(thamChieuTruoc)).Should().BeTrue("tệp mặt trước phải tồn tại trước khi xoá");
        (await MoDuocAsync(thamChieuSau)).Should().BeTrue("tệp mặt sau phải tồn tại trước khi xoá");

        var authed = _factory.CreateAuthenticatedClient(userId, "Audience");
        var res = await authed.PostAsJsonAsync("/api/v1/me/data-erasure", new { CurrentPassword = matKhau });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await MoDuocAsync(thamChieuTruoc)).Should().BeFalse(
            "xoá dữ liệu cá nhân phải xoá chính tệp ảnh mặt trước căn cước, không chỉ xoá đường dẫn");
        (await MoDuocAsync(thamChieuSau)).Should().BeFalse(
            "xoá dữ liệu cá nhân phải xoá chính tệp ảnh mặt sau căn cước");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
            user.CitizenCardFrontImageUrl.Should().BeNull();
            user.CitizenCardBackImageUrl.Should().BeNull();
            user.DataErasedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task XoaDuLieuCaNhan_KhongCoAnhCanCuoc_VanChay()
    {
        // Phần lớn tài khoản chưa từng nộp căn cước: đường xoá tệp không được biến thành điều kiện
        // để xoá dữ liệu thành công.
        var email = $"erase-nofile-{Guid.NewGuid():N}@test.com";
        const string matKhau = "Nh4c!Song#2026$Bb";

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = matKhau, FullName = "Không nộp giấy tờ",
            Phone = (string?)null, AcceptTerms = true
        });

        int userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            userId = (await db.Users.SingleAsync(u => u.Email == email)).Id;
        }

        var authed = _factory.CreateAuthenticatedClient(userId, "Audience");
        var res = await authed.PostAsJsonAsync("/api/v1/me/data-erasure", new { CurrentPassword = matKhau });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<string> ChuyenVaoVungRiengTuAsync(IFileStorageService storage, string tenTep)
    {
        using var noiDung = new MemoryStream(AnhPngHopLe);
        var duongDanCongKhai = await storage.SaveImageAsync(noiDung, tenTep);
        return await storage.RelocateToPrivateAsync(duongDanCongKhai);
    }

    /// <summary>Mở được = tệp còn đó. Kho tệp ném DomainException khi không tìm thấy.</summary>
    private async Task<bool> MoDuocAsync(string thamChieu)
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        try
        {
            var (stream, _) = await storage.OpenPrivateFileAsync(thamChieu);
            await stream.DisposeAsync();
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }
}

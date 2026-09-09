using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Catalog;

/// <summary>
/// MLACP-331. Xoá một thể loại đang được ai đó tham chiếu phải bị từ chối SẠCH, không nổ ở database.
///
/// Chốt "đang được sử dụng" kiểm ba bảng: buổi diễn gắn thẻ, nghệ sĩ gắn thẻ, và sở thích yêu thích.
/// MLACP-330 thêm bảng thứ tư — thể loại người dùng đánh dấu KHÔNG thích — với khoá ngoại
/// <c>Restrict</c>, nhưng không thêm vào chốt này.
///
/// Hậu quả là một thể loại chỉ bị đánh dấu "không thích" (chưa ai gắn vào buổi diễn, chưa ai thích)
/// sẽ đi lọt qua chốt rồi bị chính database chặn lại.
///
/// <b>Hậu quả đó KHÔNG phải lỗi 500</b> — <c>GlobalExceptionHandler</c> đã ánh xạ
/// <c>DbUpdateException</c> thành 409 sẵn. Khác biệt thật nằm ở hai chỗ: Admin nhận câu chung chung
/// "Dữ liệu đã tồn tại hoặc xung đột, vui lòng thử lại" thay vì biết chính xác vì sao, và mỗi lần
/// như vậy hệ thống ghi một dòng log mức ERROR cho một tình huống hoàn toàn lường trước được.
///
/// Vì cả hai đường đều trả 409, <b>so status code không phân biệt được gì</b> — bài kiểm tra phải
/// đọc nội dung thông báo. Bản đầu tiên tôi viết chỉ so status và vẫn xanh khi đã tắt bản vá.
/// </summary>
[Collection("Integration")]
public sealed class TaxonomyDeleteGuardTests
{
    private readonly ApiFactory _factory;

    public TaxonomyDeleteGuardTests(ApiFactory factory) => _factory = factory;

    private async Task<int> LonelyGenreAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var genre = new MusicGenre { Name = $"TL-{Guid.NewGuid():N}"[..14] };
        db.Genres.Add(genre);
        await db.SaveChangesAsync();
        return genre.Id;
    }

    private async Task<int> ListenerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"tax-{Guid.NewGuid():N}@test.com",
            FullName = "Khan Gia",
            Role = UserRole.Audience,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private Task<HttpResponseMessage> DeleteGenreAsync(int genreId)
        => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .DeleteAsync($"/api/v1/admin/genres/{genreId}");

    [Fact]
    public async Task AGenreSomeoneMarkedAsDislikedCannotBeDeleted()
    {
        // Thể loại này không gắn vào buổi diễn nào, không nghệ sĩ nào, không ai thích — chỉ đúng
        // một người đánh dấu không thích. Đó là trường hợp duy nhất lọt qua chốt cũ.
        var genreId = await LonelyGenreAsync();
        var userId = await ListenerAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new UserDislikedGenre { UserId = userId, GenreId = genreId });
            await db.SaveChangesAsync();
        }

        var res = await DeleteGenreAsync(genreId);
        var body = await res.Content.ReadAsStringAsync();

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body.Should().Contain("đang được sử dụng",
            "phải nói rõ vì sao không xoá được — cả hai đường đều trả 409 nên chỉ nội dung mới " +
            "phân biệt được chốt nghiệp vụ với việc để database ném ra");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Genres.AnyAsync(g => g.Id == genreId))
                .Should().BeTrue("từ chối rồi thì thể loại phải còn nguyên");
        }
    }

    [Fact]
    public async Task AGenreNobodyReferencesIsStillDeletable()
    {
        // Chốt giữ chiều ngược lại: thêm điều kiện không được làm mọi thứ hoá bất khả xoá.
        var genreId = await LonelyGenreAsync();

        var res = await DeleteGenreAsync(genreId);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Genres.AnyAsync(g => g.Id == genreId)).Should().BeFalse();
    }
}

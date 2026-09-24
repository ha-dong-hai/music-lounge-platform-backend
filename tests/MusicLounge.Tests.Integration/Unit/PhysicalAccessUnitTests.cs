using FluentAssertions;
using MusicLounge.Application.Tickets;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Tests.Integration.Unit;

/// <summary>
/// T-TST-11. Unit test theo mẫu <c>Report5_Unit Test.xls</c> (1 sheet = 1 hàm, ca đánh mã UTCIDnn,
/// phân loại N/A/B = Normal/Abnormal/Boundary). Bảng quyết định đầy đủ nằm ở
/// <c>docs/test/unit/PhysicalAccess.IsOffered.md</c>; tên ca ở đây mang đúng mã UTCID để đối chiếu
/// hai chiều giữa tài liệu và mã.
///
/// <para><b>Vì sao đây là unit test thật:</b> <see cref="PhysicalAccess.IsOffered"/> chỉ đọc hai giá
/// trị enum, không chạm cơ sở dữ liệu, không qua HTTP, không cần <c>ApiFactory</c>. Toàn bộ không
/// gian đầu vào là 2 × 3 = 6 tổ hợp nên bảng quyết định phủ được <b>hết</b>, không phải lấy mẫu.</para>
///
/// <para><b>TRẦN GIỚI HẠN — đặt file trong project `MusicLounge.Tests.Integration`:</b> dự án chưa có
/// project unit riêng. Tách project mới lúc này buộc phải sinh lại <c>packages.lock.json</c> (khoá ở
/// <c>Directory.Build.props</c>) ngay trước kỳ nộp, đổi lấy rủi ro hỏng build cho cả đội để lấy một
/// cái tên đẹp hơn. Các ca ở đây <b>không</b> dùng fixture <c>[Collection("Integration")]</c> nên
/// không dựng host và không đụng SQLite — chúng là unit test nằm nhờ trong project tích hợp.
/// <b>Đường nâng cấp:</b> khi có nhịp rảnh, tạo <c>tests/MusicLounge.Tests.Unit</c>, thêm vào
/// <c>MusicLounge.sln</c>, chạy <c>dotnet restore --force-evaluate</c> để sinh lockfile mới, rồi
/// chuyển nguyên thư mục <c>Unit/</c> sang.</para>
/// </summary>
public sealed class PhysicalAccessUnitTests
{
    private static LoungeShow ShowWith(LoungeShowFormat format) => new()
    {
        Name = "Đêm nhạc Trịnh",
        Description = "Buổi diễn dùng cho unit test",
        Format = format,
        ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
        ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3).AddHours(2)
    };

    // ---------- UTCID01..UTCID06: bảng quyết định phủ toàn bộ 2 × 3 tổ hợp ----------

    [Theory]
    // UTCID01 — N: vé vào cửa cho buổi diễn offline, ca dùng nhiều nhất.
    [InlineData("UTCID01", AccessType.Physical, LoungeShowFormat.Offline, true)]
    // UTCID02 — B: vé vào cửa cho buổi diễn Hybrid. Hybrid bán CẢ hai loại vé nên vẫn còn vé vào cửa;
    // đây là cạnh sát ranh giới với UTCID03, và là chỗ dễ viết nhầm thành false nhất.
    [InlineData("UTCID02", AccessType.Physical, LoungeShowFormat.Hybrid, true)]
    // UTCID03 — B: tổ hợp DUY NHẤT cho kết quả false trong cả bảng. Chính là quy tắc của MLACP-383.
    [InlineData("UTCID03", AccessType.Physical, LoungeShowFormat.Online, false)]
    // UTCID04..06 — N: vé xem trực tuyến không bị hình thức buổi diễn chặn, kể cả buổi diễn Offline
    // (điểm bán khác sẽ quyết định có hạng vé livestream hay không, không phải hàm này).
    [InlineData("UTCID04", AccessType.Livestream, LoungeShowFormat.Offline, true)]
    [InlineData("UTCID05", AccessType.Livestream, LoungeShowFormat.Online, true)]
    [InlineData("UTCID06", AccessType.Livestream, LoungeShowFormat.Hybrid, true)]
    public void IsOffered_TraDungTheoBangQuyetDinh(
        string utcid, AccessType accessType, LoungeShowFormat format, bool mongDoi)
    {
        PhysicalAccess.IsOffered(ShowWith(format), accessType)
            .Should().Be(mongDoi, "ca {0} trong docs/test/unit/PhysicalAccess.IsOffered.md", utcid);
    }

    // ---------- UTCID07..UTCID08: giá trị bất thường ----------

    /// <summary>
    /// UTCID07 — A: hình thức buổi diễn mang giá trị không có trong enum (dữ liệu hỏng, hoặc cột
    /// được ghi bằng SQL tay). Hàm phải coi là "vẫn còn bán vé vào cửa" vì nó chỉ loại đúng
    /// <see cref="LoungeShowFormat.Online"/> — ghi lại hành vi thật chứ không phải mong muốn:
    /// nếu sau này đổi sang danh sách trắng thì ca này phải đỏ và được xem xét lại, đó là ý đồ.
    /// </summary>
    [Fact]
    public void UTCID07_HinhThucNgoaiEnum_VanCoiLaConBanVeVaoCua()
    {
        PhysicalAccess.IsOffered(ShowWith((LoungeShowFormat)99), AccessType.Physical)
            .Should().BeTrue();
    }

    /// <summary>
    /// UTCID08 — A: buổi diễn null. Với vé xem trực tuyến, biểu thức ngắn mạch ở vế đầu nên hàm trả
    /// true mà không hề đọc <c>show.Format</c>; với vé vào cửa thì ném <see cref="NullReferenceException"/>.
    /// Hai nửa của cùng một ca, vì "null có nổ hay không" phụ thuộc vào thứ tự ngắn mạch — thứ rất dễ
    /// mất khi ai đó sắp xếp lại điều kiện cho "dễ đọc".
    /// </summary>
    [Fact]
    public void UTCID08_BuoiDienNull_NganMachOVeDau_ChiNoVoiVeVaoCua()
    {
        PhysicalAccess.IsOffered(null!, AccessType.Livestream).Should().BeTrue();

        var act = () => PhysicalAccess.IsOffered(null!, AccessType.Physical);
        act.Should().Throw<NullReferenceException>();
    }

    /// <summary>
    /// UTCID09 — N: câu từ chối gửi người dùng phải là tiếng Việt và nói đúng việc đã xảy ra
    /// (backend chưa có i18n). Ca này giữ cho câu chữ không trôi khi ai đó sửa thông điệp.
    /// </summary>
    [Fact]
    public void UTCID09_CauTuChoi_GiuNguyenVanTiengViet()
    {
        PhysicalAccess.NoLongerOffered.Should().Be(
            "Buổi diễn này đã chuyển sang hình thức online — không còn bán vé vào cửa.");
    }
}

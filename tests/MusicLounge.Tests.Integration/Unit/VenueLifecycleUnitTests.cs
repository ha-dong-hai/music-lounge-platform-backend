using FluentAssertions;
using MusicLounge.Application.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Tests.Integration.Unit;

/// <summary>
/// T-TST-11. Unit test theo mẫu <c>Report5_Unit Test.xls</c> cho <see cref="VenueLifecycle.CanOperate"/>.
/// Bảng quyết định: <c>docs/test/unit/VenueLifecycle.CanOperate.md</c>.
///
/// <para>Hàm này trả lời một câu duy nhất — "phòng trà có được mở buổi diễn, bán vé, nhận donation
/// không" — và trước MLACP-307 câu đó có <b>ba</b> đáp án khác nhau trong cùng hệ thống. Vì vậy ca
/// test không chỉ kiểm từng trạng thái, mà kiểm cả <b>tính đầy đủ</b>: mọi giá trị của
/// <see cref="LoungeStatus"/> đều phải được phân loại, để thêm một trạng thái mới mà quên xếp chỗ
/// thì có chỗ báo.</para>
/// </summary>
public sealed class VenueLifecycleUnitTests
{
    // ---------- UTCID01..UTCID06: phủ toàn bộ 6 giá trị của LoungeStatus ----------

    [Theory]
    // UTCID01 — N: trạng thái bình thường của một phòng trà đang bán vé.
    [InlineData("UTCID01", LoungeStatus.Approved, true)]
    // UTCID02 — B: ranh giới thật của quy tắc. Cảnh cáo là một vết ghi lại, KHÔNG phải lệnh dừng —
    // phòng trà bị cảnh cáo vẫn bán vé. Đây là ca dễ bị siết nhầm thành false nhất khi ai đó đọc
    // "Warned" rồi suy theo cảm giác.
    [InlineData("UTCID02", LoungeStatus.Warned, true)]
    // UTCID03 — B: hồ sơ chưa ai duyệt. Chính ca này là lỗi cũ: phòng trà Pending từng vừa nằm trong
    // danh sách công khai vừa mở bán vé và thu tiền thật.
    [InlineData("UTCID03", LoungeStatus.Pending, false)]
    // UTCID04..06 — N: ba trạng thái dừng giao dịch.
    [InlineData("UTCID04", LoungeStatus.Suspended, false)]
    [InlineData("UTCID05", LoungeStatus.Locked, false)]
    [InlineData("UTCID06", LoungeStatus.Rejected, false)]
    public void CanOperate_TraDungTheoBangQuyetDinh(string utcid, LoungeStatus status, bool mongDoi)
    {
        VenueLifecycle.CanOperate(status)
            .Should().Be(mongDoi, "ca {0} trong docs/test/unit/VenueLifecycle.CanOperate.md", utcid);
    }

    /// <summary>
    /// UTCID07 — A: giá trị ngoài enum (cột lưu dạng chuỗi, nên một bản ghi cũ hoặc một lệnh SQL tay
    /// có thể tạo ra giá trị không khớp). Mặc định phải là KHÔNG được giao dịch — chọn hướng an toàn,
    /// vì đoán sai theo chiều kia nghĩa là một phòng trà không rõ trạng thái vẫn thu được tiền thật.
    /// </summary>
    [Fact]
    public void UTCID07_TrangThaiNgoaiEnum_KhongDuocGiaoDich()
    {
        VenueLifecycle.CanOperate((LoungeStatus)99).Should().BeFalse();
    }

    /// <summary>
    /// UTCID08 — N: bài kiểm tính ĐẦY ĐỦ. Mọi giá trị của <see cref="LoungeStatus"/> phải nằm đúng
    /// một trong hai nhóm, và tổng hai nhóm phải bằng đúng số giá trị của enum. Thêm một trạng thái
    /// mới mà quên xếp chỗ thì ca này đỏ ngay — đó là thứ mà sáu ca ở trên không bắt được, vì chúng
    /// chỉ biết những giá trị đã được viết ra lúc này.
    /// </summary>
    [Fact]
    public void UTCID08_MoiTrangThaiDeuDuocPhanLoai_KhongSotGiaTriNao()
    {
        var tatCa = Enum.GetValues<LoungeStatus>();
        tatCa.Should().NotBeEmpty("quét trúng số không thì bài kiểm này xanh mà chẳng kiểm gì");

        var choPhep = tatCa.Where(VenueLifecycle.CanOperate).ToArray();
        var khongChoPhep = tatCa.Where(s => !VenueLifecycle.CanOperate(s)).ToArray();

        (choPhep.Length + khongChoPhep.Length).Should().Be(tatCa.Length);
        choPhep.Should().BeEquivalentTo(VenueLifecycle.Operating,
            "danh sách Operating là nguồn duy nhất, và nó được dùng thẳng trong truy vấn database qua Contains");
    }

    /// <summary>
    /// UTCID09 — N: câu từ chối gửi <b>người mua</b> phải giữ nguyên văn tiếng Việt và không được lộ
    /// lý do phòng trà bị đình chỉ — đó là chuyện giữa phòng trà với nền tảng (MLACP-354).
    /// </summary>
    [Fact]
    public void UTCID09_CauTuChoiChoNguoiMua_GiuNguyenVan_VaKhongLoLyDo()
    {
        VenueLifecycle.TradingPausedForBuyers.Should().Be(
            "Phòng trà của buổi diễn này hiện tạm ngừng giao dịch trên nền tảng — chưa thể mua vé hay donate lúc này.");

        VenueLifecycle.TradingPausedForBuyers.Should()
            .NotContainAny("đình chỉ", "khoá", "vi phạm", "Suspended", "Locked");
    }
}

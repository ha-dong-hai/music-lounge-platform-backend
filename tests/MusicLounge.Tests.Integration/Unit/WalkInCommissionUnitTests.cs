using FluentAssertions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Tickets;

namespace MusicLounge.Tests.Integration.Unit;

/// <summary>
/// T-TST-11. Unit test theo mẫu <c>Report5_Unit Test.xls</c> cho <see cref="WalkInCommission"/>.
/// Bảng quyết định: <c>docs/test/unit/WalkInCommission.IsEnabledAsync.md</c>.
///
/// <para><b>Đây là hàm tiền.</b> Nó quyết định vé bán tại quầy có đi qua sổ cái và có được lên lịch
/// chi trả hay không. Lệch một chiều thì nền tảng chuyển khoản cho phòng trà một khoản họ ĐÃ thu
/// tiền mặt — trả hai lần cho cùng một vé; lệch chiều kia thì sổ cái có bút toán mà không bao giờ có
/// lệnh chi. Vì vậy ca test không chỉ kiểm giá trị trả về mà kiểm cả <b>tên khoá được đọc</b>: đọc
/// nhầm khoá thì hàm vẫn trả về một giá trị hợp lệ, không có gì báo lỗi, và sai lệch chỉ lộ ra ở
/// bảng đối soát.</para>
///
/// <para>Không cần cơ sở dữ liệu: <see cref="ISystemConfigService"/> được thay bằng một bản giả ghi
/// lại tham số của lời gọi.</para>
/// </summary>
public sealed class WalkInCommissionUnitTests
{
    /// <summary>Bản giả tối thiểu: ghi lại khoá và giá trị mặc định được hỏi, trả về câu trả lời đã đặt.</summary>
    private sealed class ConfigGia(bool? giaTriTraVe) : ISystemConfigService
    {
        public string? KhoaDaHoi { get; private set; }
        public bool MacDinhDaNhan { get; private set; }

        public Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default)
        {
            KhoaDaHoi = key;
            MacDinhDaNhan = fallback;
            // giaTriTraVe == null nghĩa là khoá không có trong system_config, nên dịch vụ thật sẽ
            // trả về đúng fallback mà nơi gọi đưa xuống.
            return Task.FromResult(giaTriTraVe ?? fallback);
        }

        public Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
            => throw new NotSupportedException("ca test này không dùng tới");
        public Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
            => throw new NotSupportedException("ca test này không dùng tới");
        public Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default)
            => throw new NotSupportedException("ca test này không dùng tới");
        public void Invalidate(string key) { }
    }

    /// <summary>
    /// UTCID01 — B: khoá <c>walkin_commission_enabled</c> KHÔNG được seed, nên giá trị mặc định trong
    /// mã <b>chính là chính sách đang chạy</b> — không phải một dự phòng lý thuyết. Mặc định là TẮT
    /// theo quyết định sản phẩm 09/08/2026: tiền vé tại quầy do phòng trà thu trực tiếp.
    /// </summary>
    [Fact]
    public void UTCID01_MacDinh_LaTAT()
    {
        WalkInCommission.DefaultEnabled.Should().BeFalse();
    }

    /// <summary>
    /// UTCID02 — N: không có dòng cấu hình trong cơ sở dữ liệu ⇒ hàm phải trả về đúng mặc định TẮT,
    /// và phải hỏi xuống đúng mặc định đó chứ không phải một hằng tự chế.
    /// </summary>
    [Fact]
    public async Task UTCID02_KhongCoDongCauHinh_TraVeMacDinhTAT()
    {
        var config = new ConfigGia(null);

        (await WalkInCommission.IsEnabledAsync(config, default)).Should().BeFalse();
        config.MacDinhDaNhan.Should().Be(WalkInCommission.DefaultEnabled);
    }

    /// <summary>UTCID03 — N: cấu hình bật ⇒ true. Chỉ bật khi tiền bán tại quầy thật sự chảy về nền tảng.</summary>
    [Fact]
    public async Task UTCID03_CauHinhBat_TraVeTrue()
    {
        (await WalkInCommission.IsEnabledAsync(new ConfigGia(true), default)).Should().BeTrue();
    }

    /// <summary>UTCID04 — N: cấu hình tắt tường minh ⇒ false.</summary>
    [Fact]
    public async Task UTCID04_CauHinhTatTuongMinh_TraVeFalse()
    {
        (await WalkInCommission.IsEnabledAsync(new ConfigGia(false), default)).Should().BeFalse();
    }

    /// <summary>
    /// UTCID05 — A: ca bắt lỗi im lặng. Đọc nhầm tên khoá thì hàm vẫn chạy trơn tru và vẫn trả về
    /// một giá trị hợp lệ — không ngoại lệ, không cảnh báo — nên chỉ có bài kiểm này mới thấy.
    /// </summary>
    [Fact]
    public async Task UTCID05_PhaiDocDungKhoa_walkin_commission_enabled()
    {
        var config = new ConfigGia(true);

        await WalkInCommission.IsEnabledAsync(config, default);

        config.KhoaDaHoi.Should().Be(ConfigKeys.WalkInCommissionEnabled);
        config.KhoaDaHoi.Should().Be("walkin_commission_enabled",
            "ghi cả chuỗi thật: đổi giá trị của hằng mà quên đổi dòng trong system_config thì hai nơi trôi khỏi nhau");
    }
}

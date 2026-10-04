using FluentAssertions;
using MusicLounge.Infrastructure.Services;

namespace MusicLounge.Tests.Integration.Unit;

/// <summary>
/// MLACP-601. Lý do gợi ý của mô hình học máy được in thẳng cho khán giả, nên phải là câu người thường đọc hiểu —
/// không tên thuật toán, không từ tiếng Anh của hệ thống, không dấu cộng ghép nguồn điểm.
/// </summary>
public sealed class RecommendationReasonUnitTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LyDo_LaCauTiengVietChoNguoiXem_KhongLoTenThuatToan(bool laPhongTraDangTheoDoi)
    {
        var lyDo = MLNetRecommendationService.LyDoChoNguoiXem(laPhongTraDangTheoDoi);

        lyDo.Should().NotContainAny("Hybrid", "hybrid", "venue", "+", "hành vi người dùng", "tiêu chí riêng");
        lyDo.Should().Contain("gu nhạc");
        lyDo.Length.Should().BeLessThanOrEqualTo(70, "câu này in bằng chữ viết tay trên thẻ gợi ý, dài quá sẽ xuống nhiều dòng");
    }

    [Fact]
    public void PhongTraDangTheoDoi_ThiNoiRaDieuDo()
    {
        MLNetRecommendationService.LyDoChoNguoiXem(true).Should().Contain("đang theo dõi");
        MLNetRecommendationService.LyDoChoNguoiXem(false).Should().NotContain("đang theo dõi");
    }
}

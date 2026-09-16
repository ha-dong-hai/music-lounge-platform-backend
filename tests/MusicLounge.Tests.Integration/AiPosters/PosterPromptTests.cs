using FluentAssertions;
using MusicLounge.Application.LoungeShows.Commands.GeneratePoster;

namespace MusicLounge.Tests.Integration.AiPosters;

/// <summary>
/// MLACP-422. Poster thật đầu tiên sinh trên Azure (16/09, show "Đêm nhạc Trịnh – Hạ trắng") có chữ "Sêêm nhûc Trỹnh",
/// "Shậi triâng" và ngày giờ là dãy số vô nghĩa: prompt cũ bắt model vẽ tên chương trình, địa điểm và ngày diễn, mà
/// model tạo ảnh không viết được tiếng Việt có dấu. Nay chỉ sinh nền không chữ.
/// </summary>
public sealed class PosterPromptTests
{
    private const string TenPhongTra = "Phòng trà Sương Mai";

    [Fact]
    public void LuonYeuCauKhongChenChu()
        => PosterPrompt.Build(TenPhongTra, ["Nhạc Trịnh"], null)
            .Should().Contain("no text").And.Contain("no letters").And.Contain("no numbers");

    [Fact]
    public void KhongDuaTenChuongTrinh_NgayDien_VaoAnh()
    {
        var prompt = PosterPrompt.Build(TenPhongTra, ["Bolero", "Ấm cúng"], null);

        prompt.Should().NotContain("Đêm nhạc Trịnh").And.NotContain("2026");
        prompt.Should().Contain("Bolero").And.Contain("Ấm cúng", "thể loại và không khí vẫn là thứ tả được bằng hình");
        prompt.Should().Contain(TenPhongTra, "tên phòng trà chỉ dùng làm bối cảnh, không phải chữ vẽ lên ảnh");
    }

    [Fact]
    public void ChuaChoTrongDeGiaoDienChenTieuDe()
        => PosterPrompt.Build(TenPhongTra, [], null)
            .Should().Contain("empty space").And.Contain("title");

    [Fact]
    public void KhongCoTheLoai_VanTaoDuocPromptCoNghia()
        => PosterPrompt.Build(TenPhongTra, [], null).Should().Contain("nhạc sống");

    [Fact]
    public void YeuCauThemCuaChuPhongTra_DuocGiuLai()
        => PosterPrompt.Build(TenPhongTra, ["Acoustic"], "tông xanh đêm, có ánh nến")
            .Should().Contain("tông xanh đêm, có ánh nến");
}

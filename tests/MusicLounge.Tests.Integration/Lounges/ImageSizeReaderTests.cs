using FluentAssertions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Lounges;

/// <summary>
/// MLACP-433. Bo doc kich thuoc anh dung de chan anh khong phai anh 360. Doc sai mot dinh dang thi ca dinh dang do bi
/// tu choi oan (hoac lot qua), nen kiem du bon dinh dang khau upload cho qua, ca ba bien the WebP.
/// </summary>
public sealed class ImageSizeReaderTests
{
    private static readonly MetadataImageSizeReader Reader = new();

    public static TheoryData<string, byte[]> CacDinhDang => new()
    {
        { "PNG", AnhMau.Png(4096, 2048) },
        { "JPEG", AnhMau.Jpeg(4096, 2048) },
        { "WebP lossy (VP8)", AnhMau.WebpLossy(4096, 2048) },
        { "WebP lossless (VP8L)", AnhMau.WebpLossless(4096, 2048) },
        { "WebP mở rộng (VP8X)", AnhMau.WebpExtended(4096, 2048) },
        { "GIF", AnhMau.Gif(4096, 2048) },
    };

    [Theory]
    [MemberData(nameof(CacDinhDang))]
    public void DocDungKichThuocMoiDinhDangDuocUpload(string dinhDang, byte[] anh)
        => Reader.ReadDisplaySize(anh).Should().Be(new ImageSize(4096, 2048), dinhDang);

    [Theory]
    [InlineData(1, 4096, 2048)]
    [InlineData(3, 4096, 2048)] // xoay 180 do: khong doi rong/cao
    [InlineData(6, 2048, 4096)] // xoay 90 do: dien thoai cam doc
    [InlineData(8, 2048, 4096)]
    public void JpegCoTheXoayExif_TraKichThuocHienThi(int orientation, int rongHienThi, int caoHienThi)
        => Reader.ReadDisplaySize(AnhMau.Jpeg(4096, 2048, orientation))
            .Should().Be(new ImageSize(rongHienThi, caoHienThi));

    [Fact]
    public void KhongPhaiAnh_TraNull_KhongNemLoi()
    {
        Reader.ReadDisplaySize("day khong phai anh"u8.ToArray()).Should().BeNull();
        Reader.ReadDisplaySize([]).Should().BeNull();
        Reader.ReadDisplaySize([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]).Should().BeNull();
    }
}

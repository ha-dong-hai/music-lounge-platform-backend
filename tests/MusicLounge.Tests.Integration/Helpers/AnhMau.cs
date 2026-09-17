using System.Text;

namespace MusicLounge.Tests.Integration.Helpers;

/// <summary>
/// MLACP-433. File anh nho nhat hop le ve phan dau (header) theo dac ta tung dinh dang, voi kich thuoc tuy chon — du de
/// doc kich thuoc, khong co du lieu pixel. Du an khong co thu vien ma hoa anh, va anh that 4096×2048 thi qua nang cho test.
/// </summary>
internal static class AnhMau
{
    /// <summary>PNG: chu ky + IHDR (rong/cao big-endian) + IEND, CRC tinh dung.</summary>
    public static byte[] Png(int width, int height)
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new List<byte>();
        ihdr.AddRange(BigEndian32(width));
        ihdr.AddRange(BigEndian32(height));
        ihdr.AddRange(new byte[] { 8, 2, 0, 0, 0 }); // 8 bit, RGB, deflate, filter 0, khong interlace
        GhiChunkPng(ms, "IHDR", ihdr.ToArray());
        GhiChunkPng(ms, "IEND", []);
        return ms.ToArray();
    }

    /// <summary>JPEG: SOI + (APP1 Exif chi co the Orientation) + SOF0 (cao roi rong, big-endian) + EOI.</summary>
    public static byte[] Jpeg(int width, int height, int? exifOrientation = null)
    {
        using var ms = new MemoryStream();
        ms.Write([0xFF, 0xD8]);
        if (exifOrientation is { } o)
        {
            var tiff = new List<byte>();
            tiff.AddRange("MM\0*"u8.ToArray());            // big-endian TIFF
            tiff.AddRange(BigEndian32(8));                  // IFD0 ngay sau header
            tiff.AddRange(new byte[] { 0, 1 });             // 1 muc
            tiff.AddRange(new byte[] { 0x01, 0x12, 0, 3 }); // the 0x0112 Orientation, kieu SHORT
            tiff.AddRange(BigEndian32(1));                  // so luong 1
            tiff.AddRange(new byte[] { 0, (byte)o, 0, 0 }); // gia tri SHORT canh trai trong 4 byte
            tiff.AddRange(BigEndian32(0));                  // khong co IFD tiep
            var app1 = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
            ms.Write([0xFF, 0xE1]);
            ms.Write(BigEndian16(app1.Length + 2));
            ms.Write(app1);
        }
        ms.Write([0xFF, 0xC0]);
        ms.Write(BigEndian16(17));
        ms.WriteByte(8);
        ms.Write(BigEndian16(height));
        ms.Write(BigEndian16(width));
        ms.Write([3, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1]);
        ms.Write([0xFF, 0xD9]);
        return ms.ToArray();
    }

    /// <summary>WebP lossy "VP8 ": frame tag + ma bat dau 9D 01 2A + rong/cao 14 bit little-endian.</summary>
    public static byte[] WebpLossy(int width, int height)
        => Riff("VP8 ", [0x30, 0x01, 0x00, 0x9D, 0x01, 0x2A,
            (byte)width, (byte)(width >> 8 & 0x3F), (byte)height, (byte)(height >> 8 & 0x3F)]);

    /// <summary>WebP lossless "VP8L": chu ky 0x2F + 14 bit (rong-1) + 14 bit (cao-1).</summary>
    public static byte[] WebpLossless(int width, int height)
    {
        var bits = (uint)(width - 1) | (uint)(height - 1) << 14;
        return Riff("VP8L", [0x2F, (byte)bits, (byte)(bits >> 8), (byte)(bits >> 16), (byte)(bits >> 24)]);
    }

    /// <summary>WebP mo rong "VP8X": co + 3 byte du tru + (rong-1) va (cao-1) moi cai 3 byte little-endian.</summary>
    public static byte[] WebpExtended(int width, int height)
        => Riff("VP8X", [0, 0, 0, 0,
            (byte)(width - 1), (byte)(width - 1 >> 8), (byte)(width - 1 >> 16),
            (byte)(height - 1), (byte)(height - 1 >> 8), (byte)(height - 1 >> 16)]);

    /// <summary>GIF: "GIF89a" + rong/cao little-endian + goi co + mau nen + ti le + ket thuc.</summary>
    public static byte[] Gif(int width, int height)
        => [.. "GIF89a"u8, (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8), 0, 0, 0, 0x3B];

    private static byte[] Riff(string chunk, byte[] data)
    {
        var padded = data.Length % 2 == 0 ? data : [.. data, 0];
        var body = Encoding.ASCII.GetBytes("WEBP" + chunk)
            .Concat(BitConverter.GetBytes(data.Length)).Concat(padded).ToArray();
        return [.. "RIFF"u8, .. BitConverter.GetBytes(body.Length), .. body];
    }

    private static void GhiChunkPng(Stream s, string type, byte[] data)
    {
        s.Write(BigEndian32(data.Length));
        var typeAndData = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        s.Write(typeAndData);
        s.Write(BigEndian32((int)Crc32(typeAndData)));
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? crc >> 1 ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }

    private static byte[] BigEndian32(int v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];
    private static byte[] BigEndian16(int v) => [(byte)(v >> 8), (byte)v];
}

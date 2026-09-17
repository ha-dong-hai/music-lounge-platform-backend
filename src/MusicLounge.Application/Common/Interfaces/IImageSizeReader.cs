namespace MusicLounge.Application.Common.Interfaces;

/// <summary>
/// MLACP-433: doc kich thuoc anh tu chinh noi dung file (chi doc phan dau, khong giai ma pixel).
/// </summary>
public interface IImageSizeReader
{
    /// <summary>
    /// Kich thuoc HIEN THI: anh chup doc tren dien thoai thuong luu ngang kem the EXIF Orientation, trinh duyet xoay lai
    /// khi hien — nen phai xoay theo the do moi ra dung ti le nguoi xem thay. Null neu khong doc duoc.
    /// </summary>
    ImageSize? ReadDisplaySize(byte[] content);
}

public readonly record struct ImageSize(int Width, int Height);

namespace MusicLounge.Application.Common;

/// <summary>
/// Kiểu MIME của một ảnh, suy ra từ chính nội dung file.
///
/// Trước MLACP-302 chỗ này suy ra từ đuôi file trong URL. Điều đó đúng đúng một giai đoạn: khi ảnh
/// còn nằm trên đĩa cục bộ và URL có dạng "/uploads/abc.png". Khi kho ảnh chuyển sang Firebase, URL
/// thành ".../o/uploads%2Fabc.png?alt=media&amp;token=..." và Path.GetExtension trả về
/// ".png?alt=media&amp;token=..." — không khớp gì, rơi xuống application/octet-stream.
///
/// Hậu quả không dừng ở một chuỗi sai. Gemini từ chối mime đó, dịch vụ kiểm duyệt bắt lỗi và trả
/// null, còn cổng kiểm duyệt thì CỐ Ý fail-open ("unscored never blocks an upload") — nên ảnh được
/// đăng mà không qua kiểm duyệt. Một lớp kiểm soát nội dung bị tắt âm thầm bởi một dòng cấu hình
/// lưu trữ.
///
/// Nội dung file là thứ duy nhất không phụ thuộc vào việc ảnh đang nằm ở đâu. Chữ ký file cũng
/// chính là thứ tầng lưu trữ đã kiểm lúc upload, nên bảng dưới đây là nguồn dùng chung cho cả hai —
/// hai bảng riêng thì sớm muộn cũng lệch nhau.
/// </summary>
public static class ImageMimeTypeHelper
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Kiểu MIME đọc được từ chữ ký file, hoặc null nếu nội dung không phải định dạng nào được nhận.
    /// </summary>
    public static string? FromContent(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 8 && content[..8].SequenceEqual(PngSignature))
            return "image/png";

        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
            return "image/jpeg";

        if (content.Length >= 6 &&
            (content[..6].SequenceEqual("GIF87a"u8) || content[..6].SequenceEqual("GIF89a"u8)))
            return "image/gif";

        if (content.Length >= 12 &&
            content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";

        if (content.Length >= 4 && content[..4].SequenceEqual("glTF"u8))
            return "model/gltf-binary";

        return null;
    }

    /// <summary>
    /// Dùng cho việc kiểm duyệt ảnh: luôn trả về một chuỗi mime gửi đi được.
    ///
    /// Rơi về application/octet-stream khi không nhận ra nội dung — trường hợp đó chỉ xảy ra nếu
    /// một thứ không phải ảnh đã lọt qua được khâu upload, và lúc ấy để dịch vụ kiểm duyệt tự từ
    /// chối vẫn đúng hơn là đoán bừa một kiểu ảnh.
    /// </summary>
    public static string ForModeration(ReadOnlySpan<byte> content)
        => FromContent(content) ?? "application/octet-stream";
}

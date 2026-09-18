namespace MusicLounge.Application.Common.Exceptions;

public sealed class ValidationException : Exception
{
    /// <summary>
    /// MLACP-448: câu chung cho mọi lỗi 400 do dữ liệu gửi lên — một nguồn duy nhất cho cả FluentValidation
    /// (<c>GlobalExceptionHandler</c>) lẫn lỗi ràng buộc model của ASP.NET (<c>InvalidModelStateResponseFactory</c>).
    /// Trước đây là câu tiếng Anh viết lặp ở ba nơi. Chi tiết từng trường nằm ở <see cref="Errors"/>.
    /// </summary>
    public const string ThongBaoChung = "Dữ liệu gửi lên không hợp lệ.";

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(ThongBaoChung)
    {
        Errors = errors;
    }
}

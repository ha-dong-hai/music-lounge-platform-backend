namespace MusicLounge.Domain.Exceptions;

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }

    /// <summary>MLACP-507. Kèm dữ liệu cho biết CÁI GÌ đang xung đột (trả ra ở trường <c>errors</c> của phản hồi 409).
    /// Câu <paramref name="message"/> giữ cố định để còn tra được bản dịch (ThongDiepSongNgu tra theo nguyên câu) —
    /// chi tiết thay đổi theo dữ liệu thì để ở đây, không nội suy vào câu.</summary>
    public ConflictException(string message, object details) : base(message) => Details = details;

    public object? Details { get; }
}

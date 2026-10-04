using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Common;

/// <summary>
/// MLACP-598. Luật chung cho bộ lọc "từ ngày – đến ngày" của các danh sách quản trị: hai đầu đều tuỳ chọn, nhưng đã có cả
/// hai thì đầu "từ" không được sau đầu "đến". Trả 422 kèm câu tiếng Việt thay vì lặng lẽ trả danh sách rỗng — danh sách
/// rỗng sẽ bị đọc nhầm là "kỳ đó không có gì".
/// </summary>
public static class KhoangNgay
{
    public static void DamBaoHopLe(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new DomainException("Khoảng ngày không hợp lệ: ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
    }
}

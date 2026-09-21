using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// Luật đếm hạn mức poster AI theo tháng — MỘT nơi duy nhất.
///
/// <para>MLACP-483. Trước đây luật này chỉ nằm trong <c>GeneratePosterCommandHandler</c>, nên số còn lại chỉ xuất hiện
/// trong câu trả lời của chính lần bấm: chủ phòng trà không có cách nào biết mình còn bao nhiêu TRƯỚC khi bấm. Giao
/// diện đành hiện trần rồi để người dùng bấm mới biết — tức là bắt họ tiêu một lượt để đọc một con số.</para>
///
/// <para>Tách ra đây thay vì chép sang chỗ thứ hai: chép luật là cách chắc chắn để hai nơi trôi ra khỏi nhau, và khi
/// trôi thì màn hình báo "còn 3" trong khi máy chủ từ chối vì đã hết — người dùng không có cách nào hiểu.</para>
/// </summary>
public static class AiPosterQuota
{
    /// <summary>
    /// Trạng thái nào CHIẾM một suất trong hạn mức tháng.
    ///
    /// <para>Đơn đang chờ hoặc đang vẽ được coi là GIỮ CHỖ — nếu không thì bấm liên tục trong lúc chờ sẽ vượt trần.
    /// Đơn hỏng (Failed/Expired) thì KHÔNG tính, tức tự trả lại lượt: lỗi của nhà cung cấp không được tính vào tiền
    /// người ta đã trả (tinh thần MLACP-419).</para>
    /// </summary>
    public static bool ChiemMotSuat(AiPosterGenerationStatus status)
        => status is AiPosterGenerationStatus.Succeeded
            or AiPosterGenerationStatus.Queued
            or AiPosterGenerationStatus.Rendering;

    /// <summary>Mốc đầu tháng theo UTC — hạn mức làm mới vào đầu tháng sau.</summary>
    public static DateTimeOffset DauThang(DateTimeOffset now)
        => new(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Số suất còn lại, không bao giờ âm.</summary>
    public static int ConLai(int tran, int daDung) => Math.Max(0, tran - daDung);
}

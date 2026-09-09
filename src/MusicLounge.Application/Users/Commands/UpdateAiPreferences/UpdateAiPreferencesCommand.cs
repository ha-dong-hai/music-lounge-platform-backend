using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Commands.UpdateAiPreferences;

/// <param name="DislikedGenreIds">
/// MLACP-330. Thể loại người dùng nói thẳng là không thích. Tuỳ chọn, mặc định rỗng — nên phía gọi
/// cũ không gửi trường này thì hành vi không đổi gì.
///
/// Sở thích tiêu cực bắt buộc phải hỏi mới biết: người dùng chỉ bấm vào, chỉ xem, chỉ mua thứ họ
/// thấy thú vị, nên thứ họ không thích không để lại dấu vết nào trong hành vi. Một hệ gợi ý chỉ học
/// từ tín hiệu tích cực thì không bao giờ sửa được một suy đoán sai.
/// </param>
public sealed record UpdateAiPreferencesCommand(
    IReadOnlyList<int> GenreIds,
    IReadOnlyList<int> MoodIds,
    IReadOnlyList<int> AtmosphereIds,
    bool EnableAiConsent,
    IReadOnlyList<int>? DislikedGenreIds = null
) : ICommand;

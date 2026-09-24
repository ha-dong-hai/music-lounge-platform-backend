using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// Decides which storage backend is in play. A one-line rule, pulled out of the DI registration so
/// it can actually be asserted on — the interesting case is not "Firebase works" but "an
/// environment with no Firebase secret still starts up and stores files", which is every developer
/// machine and every CI run.
/// </summary>
internal static class FileStorageSelector
{
    /// <summary>
    /// MLACP-484: kiểm FILE có thật, không chỉ kiểm chuỗi cấu hình có rỗng không.
    ///
    /// <para>Đo được trên Azure ngày 23/09/2026. Khoá service account nằm ở <c>/home/data</c> của App
    /// Service — ổ của chính app, KHÔNG nằm trong gói deploy. Dời vùng buộc phải xoá app rồi dựng lại
    /// nên <c>/home</c> mất theo, trong khi <c>Firebase:StorageBucket</c> và
    /// <c>Firebase:CredentialsPath</c> vẫn được nạp lại nguyên vẹn từ app settings.</para>
    ///
    /// <para>Bản cũ chỉ so hai chuỗi nên vẫn trả <c>true</c> trong trạng thái đó: DI dựng
    /// <c>FirebaseFileStorageService</c>, constructor của nó gọi <c>CredentialFactory.FromFile</c>
    /// trên một file không tồn tại và ném ngay lúc dựng dependency. Hậu quả là MỌI handler nhận
    /// <see cref="IFileStorageService"/> trả 500 — kể cả sinh poster AI, vốn chẳng liên quan gì tới
    /// việc đọc khoá.</para>
    ///
    /// <para>Triệu chứng đánh lừa nhất: 500 sau ~3 giây và KHÔNG có dòng nào trong lịch sử sinh
    /// poster, vì hỏng trước cả khi handler kịp ghi bản ghi <c>AiPosterGeneration</c>. Nhìn vào thì
    /// tưởng nhà cung cấp AI lỗi, trong khi chưa hề gọi tới Google.</para>
    ///
    /// <para>Kiểm thêm một lần chạm đĩa lúc khởi động là giá rẻ, và nó giữ đúng lời hứa ghi ở chú
    /// thích của lớp này: thiếu bí mật thì lùi về đĩa cục bộ, chứ không chết. TRẦN GIỚI HẠN: chỉ kiểm
    /// file CÓ MẶT, không kiểm nội dung có phải service account hợp lệ hay không — khoá sai định dạng
    /// vẫn ném như cũ. Muốn chặn cả ca đó thì phải thử dựng credential lúc khởi động, đắt hơn nhiều
    /// và không cần cho sự cố này.</para>
    /// </summary>
    public static bool UseFirebase(FirebaseSettings settings)
        => !string.IsNullOrWhiteSpace(settings.CredentialsPath)
           && !string.IsNullOrWhiteSpace(settings.StorageBucket)
           && File.Exists(settings.CredentialsPath);
}

namespace MusicLounge.Infrastructure.Settings;

public sealed class FirebaseSettings
{
    public string ProjectId { get; init; } = string.Empty;

    // Path to the Firebase service-account JSON (docs/secrets/Firebase/*.json, gitignored) — set
    // only in *.Local.json per-environment. Empty/missing in appsettings.json and Production until
    // that environment's own secret is provisioned; FcmService treats that as "not configured" and
    // degrades to logging instead of throwing, same as SmsService for the SMS gateway.
    public string CredentialsPath { get; init; } = string.Empty;

    // Bucket Firebase Storage, ví dụ "<project>.appspot.com" hoặc "<project>.firebasestorage.app".
    // Để trống nghĩa là chưa cấu hình — khi đó file vẫn lưu lên đĩa cục bộ, đúng cách FcmService xử
    // lý khi thiếu credential, để môi trường dev và test chạy được mà không cần bí mật nào.
    public string StorageBucket { get; init; } = string.Empty;
}

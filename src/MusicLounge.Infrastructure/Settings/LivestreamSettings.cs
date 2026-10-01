namespace MusicLounge.Infrastructure.Settings;

public sealed class LivestreamSettings
{
    // MLACP-514: livestream chỉ dùng Mux (chủ dự án chốt 01/10/2026 — "không sử dụng Cloudflare nữa"). Azure đã đặt
    // Livestream__Provider=mux; mặc định này là cho máy nào KHÔNG đặt biến (máy dev, gói nộp chạy local) — trước đây
    // là "cloudflare" nên những máy đó âm thầm chạy nhà cung cấp đã bỏ. CloudflareStreamService vẫn đăng ký để dọn các
    // buổi phát cũ đã tạo bằng Cloudflare (Livestream.Provider lưu theo từng buổi). Cloudflare sinh ảnh poster là việc khác.
    public string Provider { get; init; } = "mux";
}

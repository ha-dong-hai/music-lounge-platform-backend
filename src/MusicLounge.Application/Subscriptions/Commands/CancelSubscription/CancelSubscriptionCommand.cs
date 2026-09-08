using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Subscriptions.Commands.CancelSubscription;

// Nền tảng không có auto-charge (VNPay token_pay vẫn cần OTP mỗi kỳ — xem header comment của
// RenewSubscriptionCommand), nên "hủy" ở đây không phải "tắt gia hạn tự động" mà là chấm dứt SỚM
// kỳ đã trả trước, có chủ đích, để mở khóa đăng ký gói khác ngay — đúng ý mà thông báo lỗi của
// SubscribeToPackageCommandHandler/RenewSubscriptionCommandHandler đang ám chỉ ("... hoặc hủy
// trước khi đăng ký/gia hạn gói mới"). Hiệu lực NGAY LẬP TỨC, không hoàn tiền phần thời gian
// chưa dùng (Luật Bảo vệ quyền lợi người tiêu dùng 2023: không bắt buộc hoàn tiền phần dịch vụ
// đã được cung cấp/sử dụng).
public sealed record CancelSubscriptionCommand : ICommand;

using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Subscriptions.Commands.CancelSubscription;

// MLACP-371. Nền tảng không tự thu tiền kỳ sau (VNPay token_pay vẫn cần OTP mỗi lần — xem
// RenewSubscriptionCommand), nên "huỷ" nghĩa là: không gia hạn nữa, và gói VẪN dùng được tới hết kỳ đã trả —
// như Stripe cancel_at_period_end ("allows the subscription to complete the duration of time the customer has
// already paid for"). Không hoàn tiền phần còn lại (Shopify: "no refunds are issued for any remaining period
// post-cancellation").
//
// Trước đây huỷ có hiệu lực NGAY để mở khoá đăng ký gói khác — tức muốn đổi gói thì mất trắng số ngày đã trả.
// Đổi gói nay là lệnh riêng (ChangeSubscriptionPackage) quy phần còn lại của gói cũ thành thời gian ở gói mới.
public sealed record CancelSubscriptionCommand : ICommand;

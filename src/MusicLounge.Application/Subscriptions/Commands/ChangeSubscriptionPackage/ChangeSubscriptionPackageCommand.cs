using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Subscriptions.DTOs;

namespace MusicLounge.Application.Subscriptions.Commands.ChangeSubscriptionPackage;

/// <summary>
/// MLACP-371 — đổi sang gói khác, có hiệu lực ngay khi VNPay xác nhận. Phần giá trị còn lại của gói hiện tại
/// được quy thành thời gian ở gói mới (không hoàn tiền mặt) — xem <see cref="SubscriptionTerms"/>.
/// </summary>
public sealed record ChangeSubscriptionPackageCommand(
    int PackageId,
    string ClientIpAddress
) : ICommand<SubscriptionChangeInitiationDto>;

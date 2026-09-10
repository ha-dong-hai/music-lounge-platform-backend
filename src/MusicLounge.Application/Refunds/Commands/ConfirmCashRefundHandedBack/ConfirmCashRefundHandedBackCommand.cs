using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Refunds.Commands.ConfirmCashRefundHandedBack;

/// <summary>
/// MLACP-345. Phong tra xac nhan da tra tien mat cho khach cua mot yeu cau hoan da duoc duyet — chi
/// ap cho ve ban tai quay, noi nen tang chua bao gio giu khoan tien do.
/// </summary>
public sealed record ConfirmCashRefundHandedBackCommand(int RefundRequestId) : ICommand;

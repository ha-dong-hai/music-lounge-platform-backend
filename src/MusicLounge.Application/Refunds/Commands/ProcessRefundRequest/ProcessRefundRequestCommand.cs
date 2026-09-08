using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Refunds.Commands.ProcessRefundRequest;

public sealed record ProcessRefundRequestCommand(
    int RefundRequestId,
    string Decision,           // "Approved" | "Rejected"
    decimal? ApprovedAmount,   // null on Approved => defaults to AmountRequested
    string ClientIpAddress     // VNPay refund API requires the initiating server's IP
) : ICommand;

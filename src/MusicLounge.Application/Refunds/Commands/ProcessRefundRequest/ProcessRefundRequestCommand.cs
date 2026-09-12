using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Refunds.Commands.ProcessRefundRequest;

public sealed record ProcessRefundRequestCommand(
    int RefundRequestId,
    string Decision,           // "Approved" | "Rejected"
    decimal? ApprovedAmount,   // null on Approved => defaults to AmountRequested
    string ClientIpAddress,    // VNPay refund API requires the initiating server's IP
    string? ResolutionNote = null,  // MLACP-342: ly do cua Admin, tuy chon
    // MLACP-348: chi AutoApproveOverdueRefundsJob dat co nay. Khong co tren body cua API — controller
    // tu dung command tu cac truong rieng cua no, nen khong ai goi HTTP dat duoc gia tri nay.
    bool AutoApproved = false,
    // MLACP-384: ma chuyen khoan Admin da tu chuyen cho nguoi mua khi giao dich qua han VNPay nhan lenh hoan.
    string? ManualTransferReference = null
) : ICommand;

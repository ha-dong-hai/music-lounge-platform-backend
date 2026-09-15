namespace MusicLounge.Application.Tickets.DTOs;

/// <param name="Tickets">
/// MLACP-402. Vé bán tại quầy không có người mua (BuyerId null), nên không màn nào của khán giả hiện được mã QR của nó — quầy
/// phải in vé ngay lúc bán. Trước đây kết quả chỉ có TicketIds và không API nào trả mã QR, nên vé bán tại quầy không bao giờ
/// check-in được. TicketIds giữ nguyên cho client cũ.
/// </param>
public sealed record WalkInSaleResultDto(
    int PaymentId,
    decimal Amount,
    Guid[] TicketIds,
    IReadOnlyList<WalkInTicketDto> Tickets);

public sealed record WalkInTicketDto(Guid TicketId, string QrCode);

using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Settlements;
using MusicLounge.Application.Users.DTOs;

namespace MusicLounge.Application.Users.Queries.GetOwnerTransactionHistory;

internal sealed class GetOwnerTransactionHistoryQueryHandler
    : IRequestHandler<GetOwnerTransactionHistoryQuery, PaginatedResult<OwnerTransactionDto>>
{
    private readonly ILedgerEntryRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public GetOwnerTransactionHistoryQueryHandler(
        ILedgerEntryRepository repo, ICurrentUserService currentUser, IUnitOfWork uow)
    {
        _repo = repo;
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<PaginatedResult<OwnerTransactionDto>> Handle(
        GetOwnerTransactionHistoryQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        var result = await _repo.GetOwnerHistoryAsync(
            _currentUser.UserId, request.Type, request.From, request.To, page, size, ct);

        var items = await WithTitlesAsync(result.Items, ct);
        return new PaginatedResult<OwnerTransactionDto>(items, result.Page, result.PageSize, result.TotalCount);
    }

    // MLACP-655: dịch từng bút toán sang câu chủ phòng trà đọc được. Trước đây trang "Tiền và quyết toán" in thẳng
    // Description nội bộ: "Settlement #01a10b35-… payout", "Donate #01a10ae8-… — chặng 2, trả nghệ sĩ (88% gross)" —
    // chủ phòng trà không biết khoản nào của buổi nào (chủ dự án 05/10/2026: "xem không hiểu gì cả").
    // MLACP-658: phần dựng câu chuyển sang Settlements/OwnerMoneyTitles để danh sách quyết toán gần đây dùng chung.
    private async Task<List<OwnerTransactionDto>> WithTitlesAsync(
        IReadOnlyList<OwnerTransactionDto> rows, CancellationToken ct)
    {
        Guid? IdOf(OwnerTransactionDto r) => Guid.TryParse(r.ReferenceId, out var g) ? g : null;
        List<Guid> IdsOfType(string type) => rows
            .Where(r => string.Equals(r.Type, type, StringComparison.OrdinalIgnoreCase))
            .Select(IdOf).Where(g => g.HasValue).Select(g => g!.Value).Distinct().ToList();

        var titles = await OwnerMoneyTitles.LoadAsync(
            _uow, IdsOfType(LedgerReferenceTypes.Settlement), IdsOfType(LedgerReferenceTypes.Donation), ct);

        string TitleOf(OwnerTransactionDto r)
        {
            var id = IdOf(r);
            return r.Type.ToLowerInvariant() switch
            {
                LedgerReferenceTypes.Settlement => id is { } sid ? titles.Settlement(sid) : "Tiền quyết toán",
                // Bút toán ghi Nợ (số âm) của donation trên sổ chủ phòng trà = chặng 2: chủ chuyển cho nghệ sĩ.
                LedgerReferenceTypes.Donation => id is { } did ? titles.Donation(did, outgoing: r.Amount < 0) : "Tiền ủng hộ nghệ sĩ",
                LedgerReferenceTypes.Refund => "Thu hồi tiền vé đã hoàn cho khách",
                LedgerReferenceTypes.FnbOrder => "Tiền đồ uống",
                LedgerReferenceTypes.Payment => "Tiền vé",
                LedgerReferenceTypes.Subscription => "Gói dịch vụ",
                _ => "Giao dịch khác",
            };
        }

        return rows.Select(r => r with { Title = TitleOf(r) }).ToList();
    }
}

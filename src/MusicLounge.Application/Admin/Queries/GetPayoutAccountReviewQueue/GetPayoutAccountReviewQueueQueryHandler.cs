using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Settlements;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Admin.Queries.GetPayoutAccountReviewQueue;

internal sealed class GetPayoutAccountReviewQueueQueryHandler
    : IRequestHandler<GetPayoutAccountReviewQueueQuery, PaginatedResult<PayoutAccountReviewItemDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IPiiEncryptionService _pii;

    public GetPayoutAccountReviewQueueQueryHandler(IUnitOfWork uow, IPiiEncryptionService pii)
    {
        _uow = uow;
        _pii = pii;
    }

    public async Task<PaginatedResult<PayoutAccountReviewItemDto>> Handle(
        GetPayoutAccountReviewQueueQuery request, CancellationToken ct)
    {
        // Chỉ tài khoản của phòng trà: lệnh duyệt từ chối tài khoản của nghệ sĩ ("do chính nghệ sĩ xác
        // nhận qua liên kết gửi email"), nên đưa chúng vào hàng đợi chỉ tạo ra những dòng bấm vào là lỗi.
        var accounts = (await _uow.Repository<BankAccount, int>().FindAsync(
                b => b.OwnerType == BankAccountOwnerType.Lounge && b.IsVerified == request.Verified, ct))
            .OrderBy(b => b.CreatedAt)   // cũ nhất trước, như mọi hàng đợi duyệt khác của hệ thống
            .ToList();

        var page = accounts
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // OwnerId của BankAccount là đa hình (lounge.id hoặc performer.id) nên không có khoá ngoại để
        // Include — nạp phòng trà và chủ phòng trà của đúng trang này, không nạp cả bảng.
        var loungeIds = page.Select(b => b.OwnerId).Distinct().ToList();
        var lounges = (await _uow.Repository<MusicLoungeEntity, int>().FindAsync(
                l => loungeIds.Contains(l.Id), ct))
            .ToDictionary(l => l.Id);

        var ownerIds = lounges.Values.Select(l => l.OwnerId).Distinct().ToList();
        var owners = (await _uow.Repository<User, int>().FindAsync(u => ownerIds.Contains(u.Id), ct))
            .ToDictionary(u => u.Id);

        var items = new List<PayoutAccountReviewItemDto>(page.Count);
        foreach (var account in page)
        {
            lounges.TryGetValue(account.OwnerId, out var lounge);
            User? owner = null;
            if (lounge is not null) owners.TryGetValue(lounge.OwnerId, out owner);

            // Ba điều kiện mà LỆNH duyệt sẽ kiểm lại; tính sẵn ở đây để người duyệt thấy trước khi bấm,
            // thay vì bấm rồi nhận lỗi. Dùng CHÍNH hàm mà lệnh duyệt dùng, không viết lại phép so tên —
            // viết lại là hai nơi hiểu "trùng tên" theo hai kiểu.
            var expected = owner is null ? null : PayoutAccountName.ExpectedFor(owner);
            var soTaiKhoan = _pii.TryDecrypt(account.AccountNumber);

            items.Add(new PayoutAccountReviewItemDto(
                account.Id,
                lounge?.Id ?? account.OwnerId,
                lounge?.Name ?? "(phòng trà không còn tồn tại)",
                owner?.Id ?? 0,
                owner?.FullName ?? "(không tìm thấy chủ phòng trà)",
                account.BankName,
                Mask(soTaiKhoan),
                soTaiKhoan is null,
                account.AccountHolder,
                expected?.Name,
                !string.IsNullOrWhiteSpace(expected?.Name)
                    && PayoutAccountName.Matches(account.AccountHolder, expected.Name!),
                owner?.CitizenCardReviewStatus == KycReviewStatus.Approved,
                account.IsDefault,
                account.IsVerified,
                account.CreatedAt));
        }

        return new PaginatedResult<PayoutAccountReviewItemDto>(
            items, request.Page, request.PageSize, accounts.Count);
    }

    /// <summary>Bốn số cuối, giống cách hàng đợi duyệt CCCD che số giấy tờ.</summary>
    private static string Mask(string? accountNumber)
        => string.IsNullOrWhiteSpace(accountNumber)
            ? "(không đọc được)"
            : accountNumber.Length <= 4
                ? accountNumber
                : new string('•', accountNumber.Length - 4) + accountNumber[^4..];
}

using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Performers.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Performers.Queries.LookupPerformerConfirmation;

/// <summary>
/// MLACP-364. Nghệ sĩ phải thấy đúng những gì phòng trà đã khai thay họ trước khi xác nhận: tài khoản
/// nào (số đã che), hay khoản tiền nào, mã chuyển khoản nào. Số tài khoản chỉ hiện 4 số cuối — đủ để
/// nghệ sĩ nhận ra tài khoản của mình, không đủ để người cầm được liên kết biết cả số.
/// </summary>
internal sealed class LookupPerformerConfirmationQueryHandler
    : IRequestHandler<LookupPerformerConfirmationQuery, PerformerConfirmationDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IPiiEncryptionService _pii;

    public LookupPerformerConfirmationQueryHandler(IUnitOfWork uow, IPiiEncryptionService pii)
    {
        _uow = uow;
        _pii = pii;
    }

    public async Task<PerformerConfirmationDto> Handle(LookupPerformerConfirmationQuery request, CancellationToken ct)
    {
        var confirmation = await PerformerConfirmations.FindByTokenAsync(_uow, request.Token, ct);
        var performer = await _uow.Repository<Performer, int>().GetByIdAsync(confirmation.PerformerId, ct)
            ?? throw new NotFoundException(nameof(Performer), confirmation.PerformerId);

        string? bankName = null, masked = null, holder = null, paymentRef = null, showName = null, venueName = null;
        decimal? amount = null;
        var outdated = false;

        if (confirmation.Purpose == PerformerConfirmationPurpose.BankAccount && confirmation.BankAccountId is int accountId)
        {
            var account = await _uow.Repository<BankAccount, int>().GetByIdAsync(accountId, ct);
            if (account is null)
            {
                outdated = true;
            }
            else
            {
                outdated = PerformerConfirmations.FingerprintOf(account) != confirmation.BankAccountFingerprint;
                bankName = account.BankName;
                holder = account.AccountHolder;
                masked = PerformerConfirmations.MaskAccountNumber(_pii.Decrypt(account.AccountNumber));
            }
        }
        else if (confirmation.Purpose == PerformerConfirmationPurpose.DonationReceipt
                 && confirmation.DonationId is int donationId
                 && await _uow.Repository<Donation, int>().GetByIdAsync(donationId, ct) is { } donation)
        {
            paymentRef = donation.PaymentRef;
            // Đúng số tiền phòng trà đã báo chuyển, lấy từ nhật ký bằng chứng — không tính lại.
            amount = (await _uow.Repository<DonationEvent, long>().FindAsync(
                    e => e.DonationId == donationId && e.EventType == DonationEventType.VenueReportedPaid, ct))
                .OrderByDescending(e => e.Sequence)
                .FirstOrDefault()?.Amount;

            var performance = await _uow.Repository<Performance, int>().GetByIdAsync(donation.PerformanceId, ct);
            var show = performance is null ? null
                : await _uow.Repository<LoungeShow, int>().GetByIdAsync(performance.LoungeShowId, ct);
            var lounge = show is null ? null
                : await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct);
            showName = show?.Name;
            venueName = lounge?.Name;
        }

        var state = confirmation.UsedAt is not null ? "Used"
            : DateTimeOffset.UtcNow > confirmation.ExpiresAt ? "Expired"
            : outdated ? "Outdated"
            : "Open";

        return new PerformerConfirmationDto(
            confirmation.Purpose.ToString(), performer.Name, confirmation.ExpiresAt, state,
            confirmation.Outcome?.ToString(), bankName, masked, holder, amount, paymentRef, showName, venueName);
    }
}

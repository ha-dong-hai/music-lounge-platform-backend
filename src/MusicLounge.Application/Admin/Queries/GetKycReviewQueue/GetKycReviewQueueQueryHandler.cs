using MediatR;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Admin.Queries.GetKycReviewQueue;

internal sealed class GetKycReviewQueueQueryHandler
    : IRequestHandler<GetKycReviewQueueQuery, PaginatedResult<KycReviewItemDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IPiiEncryptionService _piiEncryption;

    public GetKycReviewQueueQueryHandler(IUnitOfWork uow, IPiiEncryptionService piiEncryption)
    {
        _uow = uow;
        _piiEncryption = piiEncryption;
    }

    public async Task<PaginatedResult<KycReviewItemDto>> Handle(
        GetKycReviewQueueQuery request, CancellationToken ct)
    {
        var status = request.Status;

        // A user belongs in the queue if EITHER document sits at the requested status — the two are
        // submitted independently, and waiting for both before showing anything would hide a card
        // that has been ready for review for a week.
        var candidates = await _uow.Repository<User, int>().FindAsync(
            u => u.CitizenCardReviewStatus == status || u.TaxProfileReviewStatus == status, ct);

        // Sorted client-side: the SQLite provider used in tests cannot ORDER BY DateTimeOffset, a
        // limitation this codebase works around the same way everywhere it sorts by one.
        var ordered = candidates
            .OrderBy(u => u.CitizenCardSubmittedAt ?? u.TaxProfileSubmittedAt ?? DateTimeOffset.MaxValue)
            .ToList();

        var page = ordered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new KycReviewItemDto(
                u.Id,
                u.FullName,
                u.Email,
                Mask(Decrypt(u.CitizenCardNumber)),
                u.CitizenCardSubmittedAt,
                u.CitizenCardReviewStatus?.ToString(),
                u.BusinessType?.ToString(),
                Decrypt(u.TaxCode),
                u.TaxProfileSubmittedAt,
                u.TaxProfileReviewStatus?.ToString(),
                u.BusinessType == PayeeBusinessType.Enterprise
                    && u.TaxProfileReviewStatus == KycReviewStatus.Pending))
            .ToList();

        return new PaginatedResult<KycReviewItemDto>(page, request.Page, request.PageSize, ordered.Count);
    }

    private string? Decrypt(string? ciphertext)
        => ciphertext is null ? null : _piiEncryption.Decrypt(ciphertext);

    private static string? Mask(string? cardNumber)
        => cardNumber is null || cardNumber.Length < 4
            ? cardNumber
            : new string('•', cardNumber.Length - 4) + cardNumber[^4..];
}

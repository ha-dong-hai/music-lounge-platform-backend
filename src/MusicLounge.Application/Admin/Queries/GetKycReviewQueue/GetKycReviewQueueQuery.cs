using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Admin.Queries.GetKycReviewQueue;

/// <param name="Status">Defaults to Pending — the only status with work attached to it.</param>
public sealed record GetKycReviewQueueQuery(
    KycReviewStatus Status = KycReviewStatus.Pending,
    int Page = 1,
    int PageSize = 20) : IQuery<PaginatedResult<KycReviewItemDto>>;

/// <param name="CitizenCardNumberMasked">
/// Last four digits only. Deciding a review needs the images, which have their own authenticated
/// endpoint; a list screen does not need every reviewer's browser history to hold full card numbers.
/// </param>
/// <param name="WithholdingWouldStopIfApproved">
/// Flags the reviews where approving actually changes money — an enterprise declaration is a request
/// to stop deducting tax, and it should not look like the routine case in the queue.
/// </param>
public sealed record KycReviewItemDto(
    int UserId,
    string FullName,
    string Email,
    string? CitizenCardNumberMasked,
    DateTimeOffset? CitizenCardSubmittedAt,
    string? CitizenCardReviewStatus,
    string? BusinessType,
    string? TaxCode,
    DateTimeOffset? TaxProfileSubmittedAt,
    string? TaxProfileReviewStatus,
    bool WithholdingWouldStopIfApproved);

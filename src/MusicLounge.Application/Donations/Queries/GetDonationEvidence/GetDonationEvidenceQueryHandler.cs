using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Donations.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Donations.Queries.GetDonationEvidence;

/// <summary>
/// MLACP-363. Cái Admin đưa ra khi có tranh chấp: toàn bộ các bước của một khoản donate theo đúng thứ
/// tự đã xảy ra, và câu trả lời cho câu hỏi "có dòng nào bị sửa sau khi ghi không" — tính lại chuỗi băm
/// ngay lúc xuất, không tin một cờ đã lưu từ trước.
/// </summary>
internal sealed class GetDonationEvidenceQueryHandler : IRequestHandler<GetDonationEvidenceQuery, DonationEvidenceDto>
{
    private readonly IUnitOfWork _uow;

    public GetDonationEvidenceQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<DonationEvidenceDto> Handle(GetDonationEvidenceQuery request, CancellationToken ct)
    {
        _ = await _uow.Repository<Donation, int>().GetByIdAsync(request.DonationId, ct)
            ?? throw new NotFoundException(nameof(Donation), request.DonationId);

        var events = (await _uow.Repository<DonationEvent, long>().FindAsync(
                e => e.DonationId == request.DonationId, ct))
            .OrderBy(e => e.Sequence)
            .ToList();

        var check = DonationEvidence.Verify(events);

        return new DonationEvidenceDto(
            request.DonationId,
            check.IsIntact,
            check.FirstBrokenSequence,
            events.Select(e => new DonationEventDto(
                e.Sequence, e.EventType.ToString(), e.OccurredAt, e.ActorUserId, e.Amount, e.Reference,
                e.EvidenceUrl, e.EvidenceSha256, e.Detail, e.PreviousHash, e.Hash)).ToList());
    }
}

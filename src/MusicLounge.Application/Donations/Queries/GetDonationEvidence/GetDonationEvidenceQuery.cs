using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Queries.GetDonationEvidence;

/// <summary>MLACP-363 — Admin xuất nhật ký bằng chứng của một khoản donate, kèm kết quả kiểm chuỗi băm.</summary>
public sealed record GetDonationEvidenceQuery(int DonationId) : IQuery<DonationEvidenceDto>;

using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Queries.GetPerformerDonationSummary;

/// <summary>MLACP-365 — tổng hợp sao kê công khai và chính sách donate của một nghệ sĩ (không cần đăng nhập).</summary>
public sealed record GetPerformerDonationSummaryQuery(int PerformerId) : IQuery<PerformerDonationSummaryDto>;

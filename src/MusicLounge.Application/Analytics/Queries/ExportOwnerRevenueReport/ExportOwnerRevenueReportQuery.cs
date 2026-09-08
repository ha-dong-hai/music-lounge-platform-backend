using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;

namespace MusicLounge.Application.Analytics.Queries.ExportOwnerRevenueReport;

public sealed record ExportOwnerRevenueReportQuery(
    int LoungeId,
    DateTimeOffset? From,
    DateTimeOffset? To
) : IQuery<ExportedFileDto>;

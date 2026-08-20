using MusicLounge.Application.Admin.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Admin.Queries.GetLedgerIntegrity;

public sealed record GetLedgerIntegrityQuery : IQuery<IReadOnlyList<LedgerIntegrityIssueDto>>;

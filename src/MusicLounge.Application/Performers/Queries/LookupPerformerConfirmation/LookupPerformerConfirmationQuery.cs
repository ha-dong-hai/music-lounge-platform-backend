using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Performers.DTOs;

namespace MusicLounge.Application.Performers.Queries.LookupPerformerConfirmation;

/// <summary>MLACP-364 — nghệ sĩ mở liên kết: đang được hỏi xác nhận điều gì.</summary>
public sealed record LookupPerformerConfirmationQuery(string Token) : IQuery<PerformerConfirmationDto>;

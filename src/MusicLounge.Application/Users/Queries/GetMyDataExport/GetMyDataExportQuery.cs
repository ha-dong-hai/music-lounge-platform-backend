using MediatR;
using MusicLounge.Application.Users.DTOs;

namespace MusicLounge.Application.Users.Queries.GetMyDataExport;

public sealed record GetMyDataExportQuery : IRequest<MyDataExportDto>;

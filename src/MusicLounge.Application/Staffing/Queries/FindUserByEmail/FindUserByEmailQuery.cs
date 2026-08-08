using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Staffing.DTOs;

namespace MusicLounge.Application.Staffing.Queries.FindUserByEmail;

public sealed record FindUserByEmailQuery(string Email) : IQuery<UserLookupDto>;

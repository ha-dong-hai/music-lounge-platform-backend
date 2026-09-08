using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Queries.GetLoungeBusinessLicense;

public sealed record GetLoungeBusinessLicenseQuery(int LoungeId) : IQuery<BusinessLicenseFileDto>;

public sealed record BusinessLicenseFileDto(Stream Content, string ContentType);

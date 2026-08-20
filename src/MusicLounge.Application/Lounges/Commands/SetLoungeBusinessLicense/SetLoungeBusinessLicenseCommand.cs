using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeBusinessLicense;

public sealed record SetLoungeBusinessLicenseCommand(int LoungeId, string DocumentUrl) : ICommand;

using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Performers.Commands.AddPerformerSocialLink;

public sealed record AddPerformerSocialLinkCommand(
    Guid PerformerId,
    string Platform,
    string Url,
    string? DisplayName
) : ICommand<Guid>;

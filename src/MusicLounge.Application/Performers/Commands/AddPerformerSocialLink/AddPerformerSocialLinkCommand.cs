using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Performers.Commands.AddPerformerSocialLink;

public sealed record AddPerformerSocialLinkCommand(
    int PerformerId,
    string Platform,
    string Url,
    string? DisplayName
) : ICommand<int>;

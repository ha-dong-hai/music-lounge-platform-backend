using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Performers.Commands.RemovePerformerSocialLink;

public sealed record RemovePerformerSocialLinkCommand(Guid PerformerId, Guid LinkId) : ICommand;

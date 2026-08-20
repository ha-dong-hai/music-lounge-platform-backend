using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Performers.Commands.RemovePerformerSocialLink;

public sealed record RemovePerformerSocialLinkCommand(int PerformerId, int LinkId) : ICommand;

using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Livestreams.Commands.ProcessMuxWebhook;

public sealed record ProcessMuxWebhookCommand(string RawBody, string? SignatureHeader) : ICommand<bool>;

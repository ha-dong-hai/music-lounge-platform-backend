using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Tickets.Commands.ProcessVnPayCallback;

public sealed record ProcessVnPayCallbackCommand(
    IDictionary<string, string> QueryParams) : ICommand<bool>;

using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Admin.Commands.TriggerRecurringJob;

public sealed record TriggerRecurringJobCommand(string JobId) : ICommand;

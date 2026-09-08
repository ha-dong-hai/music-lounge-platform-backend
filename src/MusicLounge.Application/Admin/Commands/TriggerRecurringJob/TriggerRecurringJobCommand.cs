using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Admin.Commands.TriggerRecurringJob;

/// <param name="JobId">Phải khớp chính xác một id đang được đăng ký — xem GET /admin/jobs.</param>
public sealed record TriggerRecurringJobCommand(string JobId) : ICommand;

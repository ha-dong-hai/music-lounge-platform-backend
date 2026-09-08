using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Admin.Commands.UpdateSystemConfig;

/// <summary>
/// The only write path into system_config. Until now there was none at all: the table was seeded by
/// migration and read at runtime, but changing a value — including the platform's commission and
/// the withheld tax rate — meant running SQL by hand, with nothing recording who did it or why.
///
/// SystemConfigHistory was built for exactly this and never written to. It already carries
/// OldValue, NewValue, ChangedBy, ChangedAt and a mandatory Note, which is what a financial audit
/// trail is asked for: not just that something changed, but what it was before and on whose
/// authority. This handler is what finally fills it.
/// </summary>
internal sealed class UpdateSystemConfigCommandHandler : IRequestHandler<UpdateSystemConfigCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;
    private readonly IAsyncKeyedLock _lock;
    private readonly ILogger<UpdateSystemConfigCommandHandler> _logger;

    public UpdateSystemConfigCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ISystemConfigService config,
        IAsyncKeyedLock @lock,
        ILogger<UpdateSystemConfigCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
        _lock = @lock;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdateSystemConfigCommand request, CancellationToken ct)
    {
        // Two Admins editing commission and tax at the same moment could each pass the combined-rate
        // check against the other's pre-change value and both commit, landing on a total neither of
        // them approved. One lock across all config writes — these are rare, deliberate operations,
        // so there is nothing to gain from finer-grained locking.
        await using var _ = await _lock.AcquireAsync("system-config-write", ct);

        var repo = _uow.Repository<SystemConfig, int>();
        var matches = await repo.FindAsync(c => c.ConfigKey == request.ConfigKey, ct);
        var config = matches.FirstOrDefault()
            ?? throw new NotFoundException(nameof(SystemConfig), request.ConfigKey);

        if (config.ConfigValue == request.ConfigValue)
            throw new DomainException(
                $"Giá trị của \"{request.ConfigKey}\" vốn đã là \"{request.ConfigValue}\" — không có gì để đổi.");

        // Fetch the sibling rates the validator may need for its cross-key rule, so the rule itself
        // stays a pure function and can be unit-tested without a database.
        var rateKeys = new[]
        {
            ConfigKeys.PlatformCommissionRate, ConfigKeys.TaxRate, ConfigKeys.PersonalIncomeTaxRate
        };
        var siblings = await repo.FindAsync(c => rateKeys.Contains(c.ConfigKey), ct);
        var otherRates = siblings
            .Where(c => c.ConfigKey != request.ConfigKey)
            .Select(c => (c.ConfigKey, Parsed: decimal.TryParse(
                c.ConfigValue, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : (decimal?)null))
            .Where(x => x.Parsed.HasValue)
            .ToDictionary(x => x.ConfigKey, x => x.Parsed!.Value);

        var error = SystemConfigValidation.Validate(
            request.ConfigKey, request.ConfigValue, config.DataType, otherRates);
        if (error is not null)
            throw new DomainException(error);

        var now = DateTimeOffset.UtcNow;
        var oldValue = config.ConfigValue;

        // History row first, and with the OLD value captured before the entity is touched — the
        // whole point of the table is to answer "what was it before", which is unanswerable once
        // the row has been overwritten.
        _uow.Repository<SystemConfigHistory, long>().Add(new SystemConfigHistory
        {
            ConfigKey = config.ConfigKey,
            OldValue = oldValue,
            NewValue = request.ConfigValue,
            EffectiveFrom = now,
            ChangedBy = _currentUser.UserId,
            ChangedAt = now,
            Note = request.Note
        });

        config.ConfigValue = request.ConfigValue;
        config.UpdatedBy = _currentUser.UserId;
        config.UpdatedAt = now;
        repo.Update(config);

        await _uow.SaveChangesAsync(ct);

        // SystemConfigService caches reads for 60s. Without this the change appears to have silently
        // not taken effect for up to a minute, which for a money rate is exactly the kind of
        // ambiguity that gets a value changed twice.
        _config.Invalidate(config.ConfigKey);

        _logger.LogWarning(
            "System config changed: Key={ConfigKey} From={OldValue} To={NewValue} " +
            "by AdminUserId={AdminUserId} Reason={Note} at {At}",
            config.ConfigKey, oldValue, request.ConfigValue, _currentUser.UserId, request.Note, now);

        return Unit.Value;
    }
}

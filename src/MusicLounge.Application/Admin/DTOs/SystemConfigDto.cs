using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Admin.DTOs;

/// <param name="IsMoneyRate">
/// True for keys that are a proportion of money. The UI should render these as percentages and warn
/// before changing them — an Admin editing a raw "0.05" has no way of telling from the value alone
/// that they are looking at the platform's entire commission.
/// </param>
public sealed record SystemConfigDto(
    string ConfigKey,
    string ConfigValue,
    ConfigDataType DataType,
    string? Description,
    bool IsMoneyRate,
    DateTimeOffset UpdatedAt,
    int? UpdatedBy,
    string? UpdatedByName);

public sealed record SystemConfigHistoryDto(
    long Id,
    string ConfigKey,
    string? OldValue,
    string NewValue,
    string Note,
    DateTimeOffset ChangedAt,
    int ChangedBy,
    string? ChangedByName);

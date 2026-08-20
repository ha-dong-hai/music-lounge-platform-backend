using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Commands.UpdateAiPreferences;

public sealed record UpdateAiPreferencesCommand(
    IReadOnlyList<int> GenreIds,
    IReadOnlyList<int> MoodIds,
    IReadOnlyList<int> AtmosphereIds,
    bool EnableAiConsent
) : ICommand;

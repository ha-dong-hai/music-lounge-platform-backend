using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// Decides which storage backend is in play. A one-line rule, pulled out of the DI registration so
/// it can actually be asserted on — the interesting case is not "Firebase works" but "an
/// environment with no Firebase secret still starts up and stores files", which is every developer
/// machine and every CI run.
/// </summary>
internal static class FileStorageSelector
{
    public static bool UseFirebase(FirebaseSettings settings)
        => !string.IsNullOrWhiteSpace(settings.CredentialsPath)
           && !string.IsNullOrWhiteSpace(settings.StorageBucket);
}

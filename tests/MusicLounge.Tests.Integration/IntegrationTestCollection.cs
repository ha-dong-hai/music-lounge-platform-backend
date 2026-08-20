namespace MusicLounge.Tests.Integration;

/// <summary>
/// Shares a single ApiFactory (and therefore a single in-memory SQLite DB + Serilog startup)
/// across all integration test classes. This avoids Serilog "logger already frozen" errors
/// caused by running Program.Main() multiple times in the same process.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationTestCollection : ICollectionFixture<ApiFactory> { }

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

internal sealed class LivestreamServiceFactory : ILivestreamServiceFactory
{
    private readonly IServiceProvider _sp;
    private readonly LivestreamSettings _settings;

    public LivestreamServiceFactory(IServiceProvider sp, IOptions<LivestreamSettings> settings)
    {
        _sp = sp;
        _settings = settings.Value;
    }

    public string ActiveProviderKey => _settings.Provider;

    public ILivestreamService GetProvider(string? providerKey = null)
        => _sp.GetRequiredKeyedService<ILivestreamService>(providerKey ?? _settings.Provider);
}

namespace MusicLounge.Application.Common.Interfaces;

public interface ILivestreamServiceFactory
{
    ILivestreamService GetProvider(string? providerKey = null);
    string ActiveProviderKey { get; }
}

namespace MusicLounge.Domain.Exceptions;

public class ExternalServiceException : Exception
{
    public ExternalServiceException(string service, string message, Exception? inner = null)
        : base($"[{service}] {message}", inner) { }
}

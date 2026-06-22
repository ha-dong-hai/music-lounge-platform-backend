namespace MusicLounge.Domain.Exceptions;

public class ExternalServiceException : Exception
{
    public string ServiceName { get; }

    public ExternalServiceException(string serviceName, string message)
        : base(message)
    {
        ServiceName = serviceName;
    }
}

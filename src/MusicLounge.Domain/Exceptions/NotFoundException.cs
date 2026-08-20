namespace MusicLounge.Domain.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string name, object key)
        : base($"'{name}' với key ({key}) không tồn tại.") { }
}

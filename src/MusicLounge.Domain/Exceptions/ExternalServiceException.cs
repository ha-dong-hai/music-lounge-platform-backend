namespace MusicLounge.Domain.Exceptions;

public class ExternalServiceException : Exception
{
    public ExternalServiceException(string service, string message, Exception? inner = null)
        : base($"[{service}] {message}", inner)
    {
        Detail = message;
    }

    // MLACP-432: thong bao KHONG kem tien to "[TenDichVu]". Message giu tien to de log van biet loi tu dau, con noi nao
    // dua loi toi nguoi dung cuoi (vd lan ghep anh that bai) thi dung Detail.
    public string Detail { get; }
}

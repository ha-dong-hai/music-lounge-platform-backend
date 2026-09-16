namespace MusicLounge.Infrastructure.Settings;

/// <summary>
/// MLACP-416: noi luu file nguoi dung (anh cong khai + anh CCCD rieng tu).
///
/// Mac dinh rong = giu nguyen hanh vi cu: luu ngay trong thu muc chay app (wwwroot/uploads va
/// App_Data/private-uploads) — tien cho dev/self-host.
///
/// Tren Azure App Service thi dat ra ngoai thu muc deploy (vd /home/data). Hai ly do:
///   1. Deploy: Kudu ghi de tung file ngay tren thu muc app dang chay. Chi khi khong con du lieu nguoi dung o do moi bat
///      duoc WEBSITE_RUN_FROM_PACKAGE (gan nguyen goi, doi goi la xong) — het canh ~2 phut tra 500 vi DLL doc do.
///   2. An toan du lieu: deploy kem --clean true tu truoc toi nay se xoa sach anh da upload va anh CCCD, nen khong ai dam dung.
/// </summary>
public sealed class StorageSettings
{
    public string RootPath { get; init; } = string.Empty;
}

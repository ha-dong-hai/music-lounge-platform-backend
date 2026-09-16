namespace MusicLounge.Infrastructure.Settings;

public sealed class CloudflareSettings
{
    public string ApiToken { get; init; } = string.Empty;
    public string AccountId { get; init; } = string.Empty;

    /// <summary>MLACP-418: de trong = FLUX.1 schnell (mien phi, ~230 anh/ngay). Doi model khi can chat luong khac.</summary>
    public string ImageModel { get; init; } = string.Empty;

    /// <summary>
    /// MLACP-424: danh sach model theo THU TU UU TIEN, moi model thanh mot mat xich du phong. Hong model dau thi
    /// tu chuyen sang model sau. De trong thi quay ve ImageModel (hoac model mac dinh) — cau hinh dang chay tren
    /// Azure dat Cloudflare__ImageModel nen khong duoc pha.
    /// </summary>
    public string[] ImageModels { get; init; } = [];
}

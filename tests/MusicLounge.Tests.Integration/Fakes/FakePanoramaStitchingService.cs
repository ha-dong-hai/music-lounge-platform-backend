using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Tests.Integration.Fakes;

/// <summary>
/// MLACP-432: handler nay tu choi ngay khi dich vu ghep anh chua cau hinh, ma moi truong test khong cau hinh — dung ban
/// that thi moi test can mot luot thu Pending deu bi chan tu dau. Ban gia nay "da cau hinh" nhung luot ghep nao cung
/// that bai, giu nguyen tinh huong "dich vu ghep anh gap su co" ma cac test cu van kiem. Ban that duoc kiem rieng o
/// PanoramaStitcherClientTests va o test tu choi som trong VenueTourStitchTests.
/// </summary>
public sealed class FakePanoramaStitchingService : IPanoramaStitchingService
{
    public const string ThongBaoLoi = "Dịch vụ ghép ảnh đang gặp sự cố (giả lập trong test).";

    public bool IsConfiguredFor(IReadOnlyList<string> imageUrls) => true;

    public Task<byte[]> StitchAsync(IReadOnlyList<string> imageUrls, CancellationToken ct = default)
        => throw new ExternalServiceException("PanoramaStitcher", ThongBaoLoi);
}

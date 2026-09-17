namespace MusicLounge.Application.Common.Interfaces;

// Not fail-open — like IAiImageGenerationService, a stitch call is a direct, user-visible action
// the Owner explicitly requested, so a failure must surface as a real error (caught by
// StitchVenueTourSceneJob and logged as a Failed VenueTourStitchAttempt row that does
// NOT count against the Owner's MaxTourScenes quota), not silently swallowed.
public interface IPanoramaStitchingService
{
    // MLACP-432: kiem truoc khi tao luot thu. Truoc day thieu cau hinh van tao luot roi that bai trong job, moi lan
    // tinh vao gioi han 20 luot tron doi cua phong tra — bam 20 lan la khoa vinh vien du chua ghep that lan nao.
    bool IsConfiguredFor(IReadOnlyList<string> imageUrls);

    // Loi nem ra la ExternalServiceException co Detail doc duoc voi chu phong tra: ly do do chinh bo anh (khong du
    // chong lan...) duoc giu nguyen, con loi he thong thay bang cau de hieu va chi tiet ky thuat nam trong log.
    Task<byte[]> StitchAsync(IReadOnlyList<string> imageUrls, CancellationToken ct = default);
}

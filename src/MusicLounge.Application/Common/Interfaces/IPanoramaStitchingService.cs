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

    // Hai loai loi, theo dung nghia da dung khap API (DomainException -> 422, ExternalServiceException -> 503):
    //   - DomainException: dich vu DA xu ly bo anh nhung khong ghep duoc (khong du chong lan...) hoac ghep qua thoi gian.
    //     Message doc duoc voi chu phong tra. Tinh vao gioi han so lan ghep vi CPU da chay.
    //   - ExternalServiceException: loi phia he thong (khong danh thuc duoc, sai cau hinh, 5xx). Detail la cau de hieu,
    //     chi tiet ky thuat nam trong log. MLACP-435: khong tinh vao gioi han.
    Task<byte[]> StitchAsync(IReadOnlyList<string> imageUrls, CancellationToken ct = default);
}

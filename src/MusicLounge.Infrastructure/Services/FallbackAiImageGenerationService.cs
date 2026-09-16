using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// MLACP-424: thu lan luot tung mat xich, chi bao hong khi TAT CA deu hong.
///
/// Truoc day he thong chon cung dung mot nha cung cap luc khoi dong, nen mot lan goi hong la tinh nang tao poster
/// chet — trong khi day la tinh nang nam trong goi subscription chu phong tra da tra tien. Do that ngay 16/09: khi
/// tai khoan Cloudflare can 10.000 neuron/ngay thi MOI model deu bi tu choi (da thu ca flux-1-schnell lan
/// stable-diffusion-xl-lightning), nen duong lui phai bac duoc sang nha cung cap khac chu khong chi sang model khac.
/// Mot mat xich = mot model cua mot nha cung cap, nen cung mot co che lo duoc ca hai truong hop.
///
/// Thu tu la thu tu uu tien: mat xich mien phi dat truoc, mat xich tra phi dat sau cung. Vi vay khi mat xich dau
/// chay duoc thi TUYET DOI khong goi cac mat xich sau — goi thua la mat tien that.
/// </summary>
public sealed class FallbackAiImageGenerationService : IAiImageGenerationService
{
    private readonly IReadOnlyList<IAiImageGenerationService> _chuoi;
    private readonly ILogger<FallbackAiImageGenerationService>? _logger;

    public FallbackAiImageGenerationService(
        IReadOnlyList<IAiImageGenerationService> chuoi,
        ILogger<FallbackAiImageGenerationService>? logger = null)
    {
        if (chuoi.Count == 0)
            throw new ArgumentException("Chuỗi dự phòng phải có ít nhất một mắt xích.", nameof(chuoi));
        _chuoi = chuoi;
        _logger = logger;
    }

    public async Task<byte[]> GenerateImageAsync(string prompt, CancellationToken ct = default)
    {
        ExternalServiceException? loiCuoi = null;
        var tatCaDeuHetHanMuc = true;

        for (var i = 0; i < _chuoi.Count; i++)
        {
            // Nguoi dung dong trinh duyet giua chung: dung han, khong dot tiep han muc cua cac mat xich con lai.
            ct.ThrowIfCancellationRequested();

            try
            {
                return await _chuoi[i].GenerateImageAsync(prompt, ct);
            }
            catch (ExternalServiceException ex)
            {
                loiCuoi = ex;
                tatCaDeuHetHanMuc &= LaHetHanMuc(ex);
                _logger?.LogWarning(
                    ex, "Tao anh AI: mat xich {ViTri}/{Tong} hong, chuyen sang mat xich tiep theo.",
                    i + 1, _chuoi.Count);
            }
        }

        // Het han muc la trang thai binh thuong cua bac mien phi, khong phai su co — noi cho chu phong tra hieu va
        // yen tam, thay vi de lot nguyen van thong bao cua nha cung cap. Thong bao goc cua Cloudflare con bao nguoi
        // doc di "nang cap len goi tra phi", ma goi do la goi cua NEN TANG chu khong phai cua ho.
        //
        // "Khong bi tru luot" la su that da kiem trong code: han muc thang chi dem lan Succeeded (MLACP-419).
        if (tatCaDeuHetHanMuc)
            throw new ExternalServiceException(
                "AiImage",
                "Hệ thống đã dùng hết lượt tạo ảnh AI miễn phí trong ngày. Bạn không bị trừ lượt nào trong gói của " +
                "mình, vui lòng thử lại vào ngày mai.",
                loiCuoi);

        throw loiCuoi!;
    }

    /// <summary>
    /// Nhan dang loi het han muc. Doi chieu tren chuoi vi cac service hien goi thang HttpClient va goi ma loi vao
    /// message; neu sau nay tach ra thanh thuoc tinh rieng thi doi cho nay.
    /// </summary>
    private static bool LaHetHanMuc(ExternalServiceException ex)
        => ex.Message.Contains("429", StringComparison.Ordinal)
           || ex.Message.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase)
           || ex.Message.Contains("free allocation", StringComparison.OrdinalIgnoreCase);
}

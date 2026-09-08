using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Queries.GetMyTaxProfile;

internal sealed class GetMyTaxProfileQueryHandler : IRequestHandler<GetMyTaxProfileQuery, TaxProfileDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;
    private readonly IPiiEncryptionService _piiEncryption;

    public GetMyTaxProfileQueryHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ISystemConfigService config,
        IPiiEncryptionService piiEncryption)
    {
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
        _piiEncryption = piiEncryption;
    }

    public async Task<TaxProfileDto> Handle(GetMyTaxProfileQuery request, CancellationToken ct)
    {
        var user = await _uow.Repository<User, int>().GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        var rates = await TaxWithholdingPolicy.ResolveForOwnerAsync(_uow, _config, user.Id, ct);
        var withholds = rates.VatRate > 0m || rates.PersonalIncomeTaxRate > 0m;

        return new TaxProfileDto(
            user.BusinessType?.ToString(),
            user.TaxCode is not null ? _piiEncryption.Decrypt(user.TaxCode) : null,
            user.TaxProfileSubmittedAt,
            user.TaxProfileVerifiedAt,
            user.TaxProfileReviewStatus?.ToString(),
            user.TaxProfileReviewNote,
            withholds,
            rates.VatRate,
            rates.PersonalIncomeTaxRate,
            Explain(user, withholds, rates));
    }

    /// <summary>
    /// Written server-side so the seller is told the same thing the ledger actually does, and so the
    /// awkward case — declared as an enterprise but still being withheld from, because nobody has
    /// checked the declaration yet — is stated plainly instead of showing as a contradiction the
    /// seller has to work out from two fields.
    /// </summary>
    private static string Explain(User user, bool withholds, TaxWithholdingRates rates)
    {
        if (user.BusinessType is null)
            return "Bạn chưa khai báo loại hình kinh doanh. Trong lúc chờ khai báo, nền tảng vẫn " +
                   "khấu trừ thuế như với hộ/cá nhân kinh doanh — đây là mức an toàn hơn cho bạn " +
                   "so với việc thiếu nghĩa vụ thuế phải nộp bù sau.";

        if (user.TaxProfileReviewStatus == KycReviewStatus.Rejected)
            return $"Hồ sơ thuế của bạn đã bị từ chối. Lý do: {user.TaxProfileReviewNote} " +
                   "Nền tảng vẫn khấu trừ thuế cho tới khi bạn nộp lại và hồ sơ được duyệt.";

        if (user.BusinessType == PayeeBusinessType.Enterprise && user.TaxProfileVerifiedAt is null)
            return "Bạn đã khai báo là doanh nghiệp và hồ sơ đang chờ duyệt. Nền tảng vẫn khấu trừ " +
                   "thuế cho tới khi hồ sơ được duyệt, vì việc dừng khấu trừ không thể chỉ dựa trên " +
                   "khai báo của chính người bán.";

        if (!withholds)
            return "Hồ sơ doanh nghiệp của bạn đã được duyệt, nên nền tảng không khấu trừ thuế cho " +
                   "các giao dịch của bạn. Bạn tự kê khai và nộp thuế theo quy định.";

        var parts = new List<string>();
        if (rates.VatRate > 0m) parts.Add($"thuế GTGT {rates.VatRate:P2}".Replace(".00%", "%"));
        if (rates.PersonalIncomeTaxRate > 0m)
            parts.Add($"thuế TNCN {rates.PersonalIncomeTaxRate:P2}".Replace(".00%", "%"));

        return $"Nền tảng khấu trừ và nộp thay {string.Join(" và ", parts)} trên doanh thu mỗi giao " +
               "dịch của bạn, theo NĐ 117/2025/NĐ-CP. Số đã khấu trừ được ghi trên từng giao dịch " +
               "và hoàn lại theo tỉ lệ nếu giao dịch bị hủy hoặc hoàn tiền.";
    }
}

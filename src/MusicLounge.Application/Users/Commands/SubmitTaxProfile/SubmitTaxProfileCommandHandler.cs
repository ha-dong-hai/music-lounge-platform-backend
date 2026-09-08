using MediatR;
using MusicLounge.Application.Auth;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.SubmitTaxProfile;

/// <summary>
/// Lets a seller declare what kind of taxpayer they are and give their tax code.
///
/// This exists because the alternative is the mistake MLACP-288 had to go back and fix on the
/// refund-policy columns: schema and reading logic in place, no way to write, and every row stuck on
/// a default nobody chose. A BusinessType column that only a database administrator can set is not
/// a classification, it is a column.
///
/// Declaring is not the same as being believed. Saying "doanh nghiệp" is, in effect, an instruction
/// to stop withholding tax, so the declaration is recorded and left unverified — TaxWithholdingPolicy
/// keeps withholding until an Admin verifies it.
/// </summary>
internal sealed class SubmitTaxProfileCommandHandler : IRequestHandler<SubmitTaxProfileCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IPiiEncryptionService _piiEncryption;

    public SubmitTaxProfileCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IPiiEncryptionService piiEncryption)
    {
        _uow = uow;
        _currentUser = currentUser;
        _piiEncryption = piiEncryption;
    }

    public async Task<Unit> Handle(SubmitTaxProfileCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var taxCode = request.TaxCode.Trim();

        // TaxCode is encrypted non-deterministically, so the duplicate check has to go through the
        // deterministic hash — same arrangement as CitizenCardNumber/CitizenCardNumberHash.
        var taxCodeHash = PasswordResetTokenHasher.Hash(taxCode);
        var takenByOther = await userRepo.AnyAsync(
            u => u.TaxCodeHash == taxCodeHash && u.Id != _currentUser.UserId, ct);
        if (takenByOther)
            throw new ConflictException("Mã số thuế này đã được đăng ký bởi tài khoản khác.");

        var user = await userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        var declared = Enum.Parse<PayeeBusinessType>(request.BusinessType, ignoreCase: true);

        // Re-declaring drops any previous verification. Otherwise a seller could get verified as a
        // hộ kinh doanh and then quietly re-declare as a doanh nghiệp, carrying the old approval
        // over to a claim nobody checked and switching their own withholding off.
        if (user.BusinessType != declared || user.TaxCodeHash != taxCodeHash)
        {
            user.TaxProfileVerifiedAt = null;
            user.TaxProfileVerifiedBy = null;
        }

        user.TaxProfileReviewStatus = KycReviewStatus.Pending;
        user.TaxProfileReviewNote = null;

        user.BusinessType = declared;
        user.TaxCode = _piiEncryption.Encrypt(taxCode);
        user.TaxCodeHash = taxCodeHash;
        user.TaxProfileSubmittedAt = DateTimeOffset.UtcNow;

        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}

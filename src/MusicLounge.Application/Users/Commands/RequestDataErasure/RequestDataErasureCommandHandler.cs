using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.RequestDataErasure;

// DSAR erasure (Luật 91/2025/QH15 + Nghị định 356/2025/NĐ-CP): the law allows up to 20 days (30 if
// a third-party processor is involved) to fully process an erasure request, with acknowledgement
// due within 2 business days — this handler processes synchronously, so both deadlines are met
// trivially (same pattern as GetMyDataExportQueryHandler for the access/portability request type).
//
// Điều 19 of the Law lets a controller decline to delete data when "pháp luật chuyên ngành không
// cho phép xóa" (a sector-specific law prohibits deletion) — Vietnam's Accounting Law mandates
// 10-year retention of accounting vouchers/ledgers/financial statements, which covers this
// platform's Payments/Settlements/LedgerEntries/Donations/Tickets. Rather than blocking the whole
// erasure request on that (which would leave Audience-role users, the overwhelming majority with
// no such records, stuck for no reason), this scrubs every identifying field on the User row in
// place and deletes the user's own pure-preference/behaviour data (which carries no independent
// retention obligation), while leaving financial/audit records' FKs pointing at the now-anonymized
// row untouched — the row no longer identifies a natural person, but referential integrity for
// legally-retained records is never at risk of a cascade/Restrict failure.
internal sealed class RequestDataErasureCommandHandler : IRequestHandler<RequestDataErasureCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<RequestDataErasureCommandHandler> _logger;
    private readonly IInferredAiProfileRepository _inferredProfile;

    public RequestDataErasureCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        ILogger<RequestDataErasureCommandHandler> logger,
        IInferredAiProfileRepository inferredProfile)
    {
        _uow = uow;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _logger = logger;
        _inferredProfile = inferredProfile;
    }

    public async Task<Unit> Handle(RequestDataErasureCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        if (user.DataErasedAt is not null)
            throw new ConflictException("Dữ liệu tài khoản đã được xóa trước đó.");

        // Local accounts must re-confirm their password before this irreversible action — Google-
        // only accounts (no PasswordHash) have nothing to confirm; the authenticated session itself
        // is the proof of control, matching common practice for OAuth-only account deletion.
        if (user.PasswordHash is not null)
        {
            if (string.IsNullOrEmpty(request.CurrentPassword) ||
                !_passwordHasher.Verify(user.PasswordHash, request.CurrentPassword))
                throw new UnauthorizedException("Mật khẩu xác nhận không đúng.");
        }

        var userId = user.Id;
        await RemoveAllAsync<Follow, int>(f => f.UserId == userId, ct);
        await RemoveAllAsync<ShowWishlist, int>(w => w.UserId == userId, ct);
        await RemoveAllAsync<UserFavouriteGenre, int>(x => x.UserId == userId, ct);
        await RemoveAllAsync<UserFavouriteMood, int>(x => x.UserId == userId, ct);
        await RemoveAllAsync<UserFavouriteAtmosphere, int>(x => x.UserId == userId, ct);
        await RemoveAllAsync<UserBehaviourLog, int>(x => x.UserId == userId, ct);
        // MLACP-330: hai tin hieu tieu cuc cung la lua chon nguoi dung tu khai, nen chung di
        // cung nhom voi so thich yeu thich — xoa tai khoan thi xoa het.
        await RemoveAllAsync<LoungeMute, int>(x => x.UserId == userId, ct);
        await RemoveAllAsync<UserDislikedGenre, int>(x => x.UserId == userId, ct);

        // Toan bo ho so he thong da suy ra: diem so hanh vi theo tung buoi dien, goi y da tinh
        // san, trong so tieu chi rieng.
        //
        // Truoc day cho nay chi xoa hai nhom sau, con UserEventScore duoc co y bo lai voi ly do no
        // la "ban ghi cache khong chua noi dung dinh danh". Ly do do sai: bang khoa theo (UserId,
        // ShowId) va cot Breakdown luu JSON ghi ro nguoi nay da du buoi dien nao, cham may sao, co
        // donate hay khong. Do la ho so hanh vi chi tiet cua mot nguoi co dinh danh — dung thu ma
        // yeu cau xoa du lieu sinh ra de xoa. No cung khong tu bien mat: job tinh lai chi upsert,
        // khong bao gio xoa, nen dong cu nam lai vinh vien sau khi nguon da bi xoa.
        await _inferredProfile.ForgetAsync(userId, ct);

        var now = DateTimeOffset.UtcNow;
        user.Email = $"deleted-user-{userId}@musiclounge.local";
        user.FullName = "Người dùng đã xóa";
        user.Phone = null;
        user.AvatarUrl = null;
        user.PasswordHash = null;
        user.GoogleId = null;
        user.PhoneVerified = false;
        user.PhoneVerificationCodeHash = null;
        user.PhoneVerificationCodeExpiresAt = null;
        user.DateOfBirth = null;
        user.AiConsent = false;
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
        user.EmailVerifiedAt = null;
        user.EmailVerificationCodeHash = null;
        user.EmailVerificationCodeExpiresAt = null;
        user.CitizenCardNumber = null;
        user.CitizenCardNumberHash = null;
        user.CitizenCardFrontImageUrl = null;
        user.CitizenCardBackImageUrl = null;
        user.CitizenCardSubmittedAt = null;
        // Same treatment as the citizen-card number above: a tax code identifies a natural person.
        // What is NOT erased is the withholding itself — Payment.TaxWithheld, the ledger journals
        // and the settlement rows are accounting records of money that actually moved, and those
        // survive an erasure request the way every other financial record does.
        user.TaxCode = null;
        user.TaxCodeHash = null;
        user.TaxProfileSubmittedAt = null;
        user.TaxProfileVerifiedAt = null;
        user.TaxProfileVerifiedBy = null;
        user.CitizenCardReviewStatus = null;
        user.CitizenCardReviewedAt = null;
        user.CitizenCardReviewedBy = null;
        user.CitizenCardReviewNote = null;
        user.TaxProfileReviewStatus = null;
        user.TaxProfileReviewNote = null;
        user.IsActive = false;
        // Revokes any JWT issued before this moment immediately (JwtBearerEvents.OnTokenValidated
        // re-checks this every request) — an erased identity must not stay usable for up to an
        // hour just because the token hasn't naturally expired yet.
        user.SecurityStamp = Guid.NewGuid();
        user.DataErasedAt = now;

        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        // Consequential action — must be traceable even though the account itself no longer
        // identifies anyone (A10 audit-trail pattern applied to the platform's own compliance need
        // to prove, later, that a given erasure request was actually processed and when).
        _logger.LogWarning("User data erased (DSAR): UserId={UserId} At={At}", userId, now);

        return Unit.Value;
    }

    private async Task RemoveAllAsync<T, TKey>(
        System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken ct)
        where T : Domain.Common.BaseEntity<TKey>
    {
        var repo = _uow.Repository<T, TKey>();
        var rows = await repo.FindAsync(predicate, ct);
        foreach (var row in rows)
            repo.Remove(row);
    }
}

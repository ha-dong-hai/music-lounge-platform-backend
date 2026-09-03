using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.BankAccounts;

// BankAccount.OwnerId is polymorphic (lounge.id or performer.id, no FK — see BankAccountConfiguration),
// so who can manage a given account depends on OwnerType: a Lounge account is managed by that
// venue's Owner; a Performer account is managed by whoever created that Performer record (§6.12 —
// Performers are a shared catalog, edit rights belong to created_by_user_id, not to every Owner
// who happens to book that performer). Admin bypasses both. Centralized so Create/Update/List
// can't drift into checking this three different ways.
internal static class BankAccountAccess
{
    public static async Task EnsureCanManageAsync(
        IUnitOfWork uow, ICurrentUserService currentUser,
        BankAccountOwnerType ownerType, int ownerId, CancellationToken ct)
    {
        if (currentUser.Role == Roles.Admin) return;

        if (ownerType == BankAccountOwnerType.Lounge)
        {
            var lounge = await uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(ownerId, ct)
                ?? throw new NotFoundException(nameof(MusicLoungeEntity), ownerId);
            if (lounge.OwnerId != currentUser.UserId)
                throw new ForbiddenException("Bạn không có quyền quản lý tài khoản ngân hàng của venue này.");
        }
        else
        {
            var performer = await uow.Repository<Performer, int>().GetByIdAsync(ownerId, ct)
                ?? throw new NotFoundException(nameof(Performer), ownerId);
            if (performer.CreatedByUserId != currentUser.UserId)
                throw new ForbiddenException(
                    "Bạn không có quyền quản lý tài khoản ngân hàng của nghệ sĩ này — chỉ người tạo hồ sơ nghệ sĩ mới có quyền.");
        }
    }
}

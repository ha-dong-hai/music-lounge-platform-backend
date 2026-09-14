namespace MusicLounge.Application.Common.Interfaces;

/// <summary>
/// MLACP-396. Phạm vi giữ khoá của một transaction: khoá lấy qua <see cref="IAsyncKeyedLock"/> hoặc
/// <see cref="IShowBookingLock"/> sau <see cref="Begin"/> chỉ được nhả ở <see cref="EndAsync"/> — tức sau khi transaction
/// đã commit hoặc rollback, không phải lúc handler dispose nó. Chỉ <c>TransactionBehavior</c> gọi hai hàm này.
/// </summary>
public interface ITransactionLockScope
{
    void Begin();

    ValueTask EndAsync();
}

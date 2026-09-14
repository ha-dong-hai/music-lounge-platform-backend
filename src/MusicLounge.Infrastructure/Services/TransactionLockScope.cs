using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// MLACP-396. Giữ các khoá một command lấy cho tới khi transaction của command đó commit hoặc rollback.
///
/// <para><c>TransactionBehavior</c> commit SAU khi handler trả về, còn handler nhả khoá bằng <c>await using</c> ngay lúc nó
/// kết thúc. Giữa hai thời điểm đó có một khe: request khác lấy được khoá và đọc dữ liệu trước khi commit — đúng cuộc đua
/// check-then-act mà khoá sinh ra để chặn (đã thấy thật ở test tăng sức chứa hạng vé đồng thời). Scoped theo request/job;
/// ngoài transaction (job Hangfire) khoá nhả ngay khi dispose như trước.</para>
/// </summary>
internal sealed class TransactionLockScope : ITransactionLockScope
{
    /// <summary>Handle trả cho handler khi scope đang giữ khoá thay nó — dispose không làm gì.</summary>
    public static readonly IAsyncDisposable AlreadyHeld = new Noop();

    private readonly List<(string Key, IAsyncDisposable Releaser)> _held = [];
    private int _depth;

    public void Begin() => _depth++;

    public async ValueTask EndAsync()
    {
        if (_depth == 0 || --_depth > 0) return;
        for (var i = _held.Count - 1; i >= 0; i--)
            await _held[i].Releaser.DisposeAsync();
        _held.Clear();
    }

    /// <summary>Khoá này đã do transaction đang mở giữ — lấy lại trong cùng transaction không được tự chờ chính mình.</summary>
    internal bool Holds(string key) => _depth > 0 && _held.Exists(h => h.Key == key);

    /// <summary>Trong transaction: scope giữ khoá tới <see cref="EndAsync"/>. Ngoài transaction: trả lại chính releaser.</summary>
    internal IAsyncDisposable Adopt(string key, IAsyncDisposable releaser)
    {
        if (_depth == 0) return releaser;
        _held.Add((key, releaser));
        return AlreadyHeld;
    }

    private sealed class Noop : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

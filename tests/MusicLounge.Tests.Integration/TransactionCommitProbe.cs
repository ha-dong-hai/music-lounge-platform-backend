using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MusicLounge.Tests.Integration;

/// <summary>
/// MLACP-396. Chạy một kiểm tra ngay trước khi một transaction commit — thời điểm duy nhất phân biệt được "khoá nhả trước
/// commit" với "khoá giữ tới commit", không cần sleep hay đua thật. Không làm gì khi không có test nào gắn kiểm tra.
/// </summary>
public sealed class TransactionCommitProbe : DbTransactionInterceptor
{
    private Func<Task>? _onCommitting;

    public void Arm(Func<Task> onCommitting) => _onCommitting = onCommitting;

    public void Disarm() => _onCommitting = null;

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (_onCommitting is { } check) await check();
        return result;
    }
}

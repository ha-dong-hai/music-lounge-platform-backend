namespace MusicLounge.Application.Common.Interfaces;

/// <summary>
/// Records failed login/OTP attempts and enforces a temporary account lockout after repeated
/// failures. Implementations must commit each write on a connection/transaction independent of
/// the calling command's own — TransactionBehavior wraps every command and rolls back all writes
/// when the handler throws, which is exactly what a failed login/OTP does, so a write sharing that
/// same transaction would have its own attempt counter erased by the rollback it's meant to survive.
/// </summary>
public interface IAuthAttemptTracker
{
    /// <summary>Null if not currently locked out, otherwise how long until the lockout expires.</summary>
    Task<TimeSpan?> GetLockoutRemainingAsync(int userId, CancellationToken ct = default);

    /// <summary>Increments the failure counter, locking the account out once it crosses the threshold.</summary>
    Task RecordFailureAsync(int userId, CancellationToken ct = default);

    /// <summary>Clears the failure counter and any active lockout — called after a verified success.</summary>
    Task ResetAsync(int userId, CancellationToken ct = default);
}

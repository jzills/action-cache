namespace ActionCache.Common.Concurrency;

/// <summary>
/// The outcome of running work under a cache lock, distinguishing "the lock was busy" from
/// anything the work itself did.
/// </summary>
/// <typeparam name="TResult">The type the work produces.</typeparam>
/// <param name="LockAcquired">
/// <see langword="true"/> when the lock was taken and <paramref name="Result"/> is the work's
/// return value; <see langword="false"/> when acquisition timed out and the work never ran.
/// </param>
/// <param name="Result">The work's result, or <see langword="default"/> when the lock was not acquired.</param>
/// <remarks>
/// Exists so callers need not infer lock failure from an exception type. Doing that conflates
/// a busy lock with a failure thrown by the work itself, and a caller that retries on the
/// former then silently runs the latter twice.
/// </remarks>
public readonly record struct CacheLockAttempt<TResult>(bool LockAcquired, TResult? Result);

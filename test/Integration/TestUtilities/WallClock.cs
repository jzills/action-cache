namespace Integration.TestUtilities;

/// <summary>
/// Waits by the same clock the cache expires against.
/// </summary>
/// <remarks>
/// A fixed sleep measures a monotonic interval, but absolute and sliding expiration are
/// anchored to <see cref="DateTimeOffset.UtcNow"/>. The two disagree whenever the host
/// clock is adjusted, and on WSL2 an NTP resync routinely moves it by seconds — a unit
/// run was observed advancing the wall clock only 1.27s across a four-second delay,
/// leaving a two-second entry unexpired. Waiting on the wall clock makes these tests
/// independent of how far, or in which direction, the clock moves.
/// </remarks>
internal static class WallClock
{
    /// <summary>
    /// Returns once <see cref="DateTimeOffset.UtcNow"/> has passed <paramref name="instant"/>.
    /// </summary>
    /// <param name="instant">The wall-clock instant to wait past.</param>
    internal static async Task WaitUntilPast(DateTimeOffset instant)
    {
        var slice = TimeSpan.FromMilliseconds(100);
        while (DateTimeOffset.UtcNow <= instant)
        {
            var remaining = instant - DateTimeOffset.UtcNow;
            await Task.Delay(remaining < slice ? slice : remaining + slice);
        }
    }
}

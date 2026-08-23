namespace Unit.TestUtilities;

/// <summary>
/// Waits by the same clock the cache expires against.
/// </summary>
/// <remarks>
/// <see cref="Task.Delay(TimeSpan)"/> measures a monotonic interval, but absolute and
/// sliding expiration are anchored to <see cref="DateTimeOffset.UtcNow"/>. The two
/// disagree whenever the host clock is adjusted, and on WSL2 an NTP resync routinely
/// moves it by seconds: a run of these tests was observed to advance the wall clock by
/// 1.27s across a four-second <c>Task.Delay</c>, leaving a two-second entry unexpired
/// and failing the assertion. Waiting on the wall clock instead of on an interval makes
/// the test independent of how far, or in which direction, the clock moves.
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

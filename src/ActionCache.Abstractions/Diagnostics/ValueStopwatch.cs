using System.Diagnostics;

namespace ActionCache.Common.Diagnostics;

/// <summary>
/// An allocation-free stopwatch for timing a single cache operation.
/// </summary>
/// <remarks>
/// A struct over <see cref="Stopwatch.GetTimestamp"/> rather than a <see cref="Stopwatch"/>
/// instance: this runs on every cache read, and an allocation per lookup is not a cost a
/// caching library should impose.
/// </remarks>
internal readonly struct ValueStopwatch
{
    private readonly long _startTimestamp;

    private ValueStopwatch(long startTimestamp) => _startTimestamp = startTimestamp;

    /// <summary>
    /// Starts timing.
    /// </summary>
    /// <returns>A stopwatch started at the current timestamp.</returns>
    internal static ValueStopwatch Start() => new(Stopwatch.GetTimestamp());

    /// <summary>
    /// The time elapsed since <see cref="Start"/>.
    /// </summary>
    internal TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startTimestamp);
}

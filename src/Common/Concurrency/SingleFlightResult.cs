namespace ActionCache.Common.Concurrency;

/// <summary>
/// The outcome of a single-flight operation.
/// </summary>
/// <typeparam name="TValue">The type of the cached value.</typeparam>
/// <param name="Value">The value produced, either by the value factory or by a re-read of the cache.</param>
/// <param name="WasCoalesced">
/// <see langword="true"/> when the value came from another request that populated the entry
/// while this one waited, meaning the caller must not execute its own action.
/// </param>
public readonly record struct SingleFlightResult<TValue>(TValue? Value, bool WasCoalesced);

using ActionCache.Common.Responses;

namespace ActionCache.Common.Caching;

/// <summary>
/// A replayed response, together with the expirations the entry should be rewritten with.
/// </summary>
/// <param name="Response">The response the replayed request produced.</param>
/// <param name="EntryOptions">
/// The expirations declared by the endpoint that produced the entry, or <see langword="null"/>
/// when it declared none and the cache's own options apply.
/// </param>
/// <remarks>
/// The expirations travel with the response rather than being read from the refresh filter,
/// which has none, and rather than being stored on the entry, which would change the payload
/// format for entries already sitting in a backend. The endpoint's declaration stays the single
/// source of truth: change the attribute and the next refresh writes by the new value.
/// </remarks>
public sealed record ActionCacheReplayResult(CachedResponse Response, ActionCacheEntryOptions? EntryOptions);

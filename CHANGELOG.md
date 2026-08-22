# Changelog

All notable changes to this project are documented here. This project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased] — 1.0.0

The first stable release. Everything below landed between 0.0.9 and 1.0.0, and the API is
considered stable from this point.

Because the pre-1.0 releases were unstable by design, this entry describes the state of the
library rather than a diff against any single earlier version. If you are upgrading from
0.0.9, read the **Breaking changes** section end to end — the shape of several things
changed.

### Security

- **Per-user responses are no longer shared between users.** Cache keys were built from
  route values and action arguments only, so two authenticated callers hitting the same
  endpoint produced an identical key and the second was served the first one's response.
  Authenticated requests now vary by caller automatically (`VaryByUserMode.Auto`).
  Anonymous requests are unaffected. Set `VaryByUser = VaryByUserMode.Never` for responses
  that genuinely do not depend on who asked.
- **Cached values no longer carry type information.** Responses were serialized as
  polymorphic `IActionResult` graphs with `TypeNameHandling`, so a cache entry named the
  types deserialization would construct, defended only by a deny-list. Values are now a
  `CachedResponse` of primitives serialized with System.Text.Json — there is nothing in a
  payload that can influence which types get created.
- **Cache keys are hashed by default**, so route values and action arguments are no longer
  recoverable by anyone who can read the cache. `options.UsePlaintextKeys()` restores
  readable keys for debugging.
- **Non-2xx responses are no longer cached**, and route values interpolated into templated
  namespaces are escaped.

### Added

- **Cache stampede protection**, on by default. Concurrent misses for one key are coalesced
  so the action runs once; opt out with `[ActionCache(SingleFlight = false)]`.
  `options.UseDistributedSingleFlight()` coordinates it across instances via Redis or SQL
  Server.
- **Vary-by**: `VaryByUser`, `VaryByHeader`, `VaryByQuery`, `VaryByClaim`, plus
  `IActionCacheKeyContributor` for dimensions the attributes cannot express.
- **`CancellationToken` on every `IActionCache` operation**, wired to
  `HttpContext.RequestAborted`, plus `options.UseOperationTimeout(...)` to bound a backend
  that hangs rather than throws.
- **Graceful degradation**: backend failures are logged and degrade to a cache miss by
  default; `options.FailClosed()` propagates them instead.
- **Metrics and tracing** on the `ActionCache` meter and activity source — request
  outcomes, operation durations, evictions and coalesced requests. Inert until something
  subscribes.
- **Separate packages per backend.** `ActionCache` now depends only on
  `ActionCache.Abstractions`; add `ActionCache.Redis`, `ActionCache.SqlServer` or
  `ActionCache.AzureCosmos` for distributed backends.

### Changed

- **Refresh replays the request that produced an entry** instead of invoking controller
  methods by reflection. Actions now run with a real `HttpContext`, model binding and
  filters. Entries that vary by request context are skipped — replaying another caller's
  request would mean impersonating them.
- **Layered caches behave like a chain**: key enumeration unions every layer (so namespace
  eviction and refresh no longer skip deeper ones), and a hit in a deeper layer is promoted
  into the faster one.
- **The memory backend has real locking.** Its namespace key index was read-modify-written
  unguarded, losing keys under concurrent writes and silently breaking eviction and refresh
  for them.
- Redis connects lazily with `AbortOnConnectFail=false`; Cosmos initializes its container
  once, lazily, without blocking startup.

### Removed

- `IActionCacheDescriptorProvider`, `ActionCacheDescriptor` and the reflection subsystem
  refresh used, along with the registration that added every controller in your application
  to DI as a scoped service.
- The `Configure*CacheOptions` properties on `ActionCacheOptions`; backends register
  themselves.

### Breaking changes

| Change | What to do |
|--------|------------|
| Backends moved to their own packages | `dotnet add package ActionCache.Redis` (or `.SqlServer` / `.AzureCosmos`). Configuration code is unchanged. |
| Authenticated endpoints cache per user | Nothing, unless you relied on the shared entry — then set `VaryByUser = VaryByUserMode.Never`. |
| Existing cache entries are unreadable | Nothing. Expect a cold cache on first deploy. |
| `IActionCache` methods take a `CancellationToken` | Only affects custom implementations. |
| `IActionCacheRefreshProvider` redefined around `ReplayAsync` | Only affects custom implementations. |
| Filter and factory constructors take new dependencies | Only affects code constructing filters directly. |
| `FileStreamResult` / `RedirectResult` are no longer cached | Nothing — they were never cached correctly. |
| `AddControllersAsServices()` no longer needed for refresh | Remove it if you added it only for that. |

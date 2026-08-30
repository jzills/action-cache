# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). While the
version stays below 1.0, breaking changes may land in a minor release — they are always
listed first in the entry.

## [0.1.0] — 2026-08-29

The first release of ActionCache as a set of packages rather than one, and the first since
`0.0.9` (February 2025). Everything below is relative to `0.0.9`.

### Breaking changes

- **The backends ship as separate packages.** `ActionCache` now contains the attributes,
  filters, key building, DI and the in-memory backend only. Redis, SQL Server and Azure
  Cosmos DB each moved to their own package. A consumer who caches in memory no longer
  inherits StackExchange.Redis, `Microsoft.Data.SqlClient`, the Cosmos SDK and
  Newtonsoft.Json — which `0.0.9` pulled in unconditionally. See *Upgrading* below; the
  registration code itself does not change.
- **`net9.0` is no longer targeted.** `0.0.9` shipped `net8.0` and `net9.0`; `0.1.0` ships
  `net8.0` and `net10.0`.
- **Cached entries from `0.0.9` are not readable.** Both the key format (now hashed) and the
  stored payload (now a rendered response envelope rather than a serialized result graph)
  changed. Entries left in a distributed backend by an older version are ignored and
  overwritten as they are re-cached — a cold cache after upgrade, not an error.
- **Stored values are no longer polymorphic.** Entries serialize through a source-generated
  `System.Text.Json` context, and nothing in a payload names a type to construct.
- **Responses vary by the authenticated user by default.** `VaryByUserMode.Auto` means two
  users hitting one `[Authorize]` endpoint no longer share a cache entry. Set
  `VaryByUser = VaryByUserMode.Never` to restore the previous behavior.
- **An endpoint may cache or have cache side effects, never both.** Combinations such as
  `[ActionCache]` with `[ActionCacheEviction]` on one endpoint now throw
  `ConflictingCacheAttributesException` at startup, listing every offending route.

### Added

- **Azure Cosmos DB backend** as `ActionCache.AzureCosmos`, with TTL-based expiry and lazy
  container initialization.
- **`ActionCache.Abstractions`**, for implementing a cache backend without depending on an
  implementation.
- **Minimal API support end to end** — `WithActionCache`, `WithActionCacheEviction` and
  `WithActionCacheRefresh`, including refresh, which previously worked for controller
  actions only.
- **Per-endpoint options for `WithActionCache`**, matching what `[ActionCache]` offers:
  expiration, vary-by and single-flight, stated as `TimeSpan` rather than milliseconds.
- **Stampede protection.** `IActionCacheSingleFlight` coalesces concurrent misses for one
  key so the origin action runs once. On by default; opt out with `SingleFlight = false`.
  `options.UseDistributedSingleFlight()` coalesces across processes over the Redis or SQL
  Server lock.
- **Vary-by keys** — `VaryByUser`, `VaryByHeader`, `VaryByQuery` and `VaryByClaim`, plus
  `IActionCacheKeyContributor` for anything else.
- **Layered backends.** Registering more than one chains them: a deeper-layer hit is
  promoted into the first layer, and key enumeration unions every layer.
- **Graceful degradation.** A backend outage degrades to a cache miss and logs a warning
  rather than failing the request. Configurable through `ActionCacheResilienceOptions`,
  including fail-closed and an operation timeout.
- **Cancellation throughout.** Every `IActionCache` method takes a `CancellationToken`, and
  the filters pass `HttpContext.RequestAborted`.
- **Observability** — structured logging and a documented telemetry contract across cache
  hits, misses, evictions, refreshes and degradation.
- **A documentation site** at <https://jzills.github.io/action-cache/>, and XML
  documentation on every public API.

### Changed

- **Refresh replays the recorded request** against the matching endpoint in its own DI
  scope, rather than reflecting over the action. Replays are marked so a refresh cannot
  recurse into itself or trip the eviction filter, and a refreshed entry keeps the
  expiration its endpoint declared instead of silently inheriting the global options.
- **Refresh skips entries that vary by the request** — replaying another caller's request
  would mean impersonating them.
- **Cache keys are hashed**, bounding key length regardless of argument size.
- **Distributed locking is production-grade** — `sp_getapplock` on SQL Server, and Lua
  scripts on Redis so its operations are atomic without a lock at all.
- **Backends connect lazily.** Redis and Cosmos initialize on first use, so an application
  no longer fails to start because a cache backend is unreachable.
- **Inter-package dependencies are pinned to an exact version.** These assemblies share
  internals and release in lockstep, so a mismatched pair could fail at runtime rather than
  at build time.
- Microsoft.Azure.Cosmos updated to 3.62.1.

### Fixed

- Only successful (2xx) results are cached. A `NotFound()` or `BadRequest()` body was
  previously cached and replayed for the whole lifetime of the entry.
- A recorded request body is replayed with the content type it arrived as, so an endpoint
  with `[Consumes]` no longer answers 415 on every refresh pass.
- A request body that cannot be faithfully replayed (XML, form data) no longer produces a
  broken replay: the entry is still cached, and refresh skips it and logs why.
- The in-memory namespace index is guarded by a singleton lock — caches are created per
  request, so a per-instance lock guarded nothing, and the read-modify-write it protects is
  not atomic in `IMemoryCache`.
- Namespace eviction in the memory backend disposed a `CancellationTokenSource` that
  in-flight requests still held, so a concurrent write threw `ObjectDisposedException` — a
  500 when fail-closed, a silently dropped cache write when fail-open. Entries written
  afterwards also carried a token no later eviction would cancel.
- The Redis expiry listener targets the database named in the connection string rather than
  database 0.
- Namespace injection through route-parameter templates.
- All build warnings; the build now treats warnings as errors.

### Upgrading from 0.0.9

Add the package for each backend you register. The registration API is unchanged — the
`Use…Cache` extensions still live in `ActionCache.Common.Extensions`, the namespace you
already import for `AddActionCache` — so no `using` and no call site needs to change:

```bash
dotnet add package ActionCache.Redis        # if you call UseRedisCache
dotnet add package ActionCache.SqlServer    # if you call UseSqlServerCache
dotnet add package ActionCache.AzureCosmos  # if you call UseAzureCosmosCache
```

`ActionCache` on its own still covers `UseMemoryCache`. Each backend package references
`ActionCache`, so you do not need to list both.

Then review the breaking changes above — in particular, expect a cold cache on first
deploy, and check whether any endpoint carries a combination of cache attributes that
startup validation now rejects.

[0.1.0]: https://github.com/jzills/action-cache/releases/tag/v0.1.0

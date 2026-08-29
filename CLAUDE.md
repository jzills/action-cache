# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a single test by name
dotnet test --filter "TestMethodName"

# Run only unit or integration tests
dotnet test test/Unit/Unit.csproj
# One framework at a time: both target the same Redis/SQL/Cosmos containers with the same
# namespaces, so concurrent runs evict each other's entries mid-test.
dotnet test test/Integration/Integration.csproj -f net8.0
dotnet test test/Integration/Integration.csproj -f net10.0
```

The library targets both `net8.0` and `net10.0`.

## What This Project Is

ActionCache is an ASP.NET Core NuGet library that adds caching to controller actions and Minimal API endpoints via attributes and filters. It supports four cache backends (Memory, Redis, SQL Server, Azure Cosmos DB) and namespace-based eviction.

## Architecture

### Entry Points

Three attributes drive all behavior:
- `ActionCacheAttribute` — cache the response
- `ActionCacheEvictionAttribute` — evict entries by namespace
- `ActionCacheRefreshAttribute` — re-invoke cached actions to warm the cache

Each attribute triggers a corresponding filter (`ActionCacheFilter`, `ActionCacheEvictionFilter`, `ActionCacheRefreshFilter`). Filters exist in two forms: MVC `IActionFilter` (in `Common/Filters/`) and Minimal APIs `IEndpointFilter` (in `Common/EndpointFilters/`). The correct type is selected at runtime via `IActionCacheFilterAbstractFactory`.

### Core Abstractions

| Interface | Purpose |
|-----------|---------|
| `IActionCache` | Core cache contract: `GetAsync`, `SetAsync`, `RemoveAsync`, `RefreshAsync`, `GetKeysAsync` |
| `IActionCacheFactory` | Creates an `IActionCache` instance per namespace |
| `IActionCacheFilterAbstractFactory` | Selects MVC vs. Minimal APIs filter implementation |
| `IActionCacheRefreshProvider` | Replays a recorded request to refresh a cache entry |

### Projects

| Project | Contents |
|---------|----------|
| `src/ActionCache.Abstractions` | `IActionCache`, `ActionCacheBase`, contexts, entry options, `Namespace`, concurrency primitives, `CachedResponse`, serializer, diagnostics |
| `src/ActionCache` | Attributes, filters, endpoint filters, keys, vary-by, DI, and the in-memory backend |
| `src/ActionCache.Redis` / `.SqlServer` / `.AzureCosmos` | One backend each |

Backends reference `ActionCache`, never the reverse. Each contributes its own
`Use…Cache` extension on `ActionCacheOptionsBuilder` (declared in
`ActionCache.Common.Extensions` so call sites need no new `using`) and registers itself via
`AddBackend`. `ActionCacheOptions` deliberately names no backend — that coupling is what
previously forced every consumer to take every backend's dependencies. A backend offering
distributed locking also calls `AddDistributedLocker`, which is how
`UseDistributedSingleFlight()` finds one.

Build with `dotnet build ActionCache.slnx`.

### Cache Backends

Each backend lives in its own directory and follows the same pattern:
- A concrete `IActionCache` implementation (e.g., `MemoryActionCache`, `RedisActionCache`)
- A factory implementing `IActionCacheFactory`
- An `IServiceCollection` extension (`AddMemoryCache`, `AddRedisCache`, etc.) wired into `AddActionCache()`

### `ActionCacheBase<TLock>`

All backends extend this abstract base, which holds the generic locking strategy:

| Backend | `TLock` | Why |
|---------|---------|-----|
| Memory | `SemaphoreSlimLock` | The namespace key index is a read-modify-write that `IMemoryCache` does not make atomic. The locker is a **singleton** — caches are created per request, so a per-instance locker would guard nothing. |
| Redis | `NullCacheLock` | Operations are atomic via Lua scripts. |
| SQL Server | `SqlServerCacheLock` | `sp_getapplock` / `sp_releaseapplock`. |
| Azure Cosmos | `NullCacheLock` | Operations are atomic. |

### Stampede Protection

`IActionCacheSingleFlight` coalesces concurrent misses for one key so the origin action
runs once. It is applied in `ActionCacheFilter` / `ActionCacheEndpointFilter` using
lock-then-recheck, on by default, opt out with `[ActionCache(SingleFlight = false)]`.
`InProcessSingleFlight` is the default; `options.UseDistributedSingleFlight()` swaps in
`DistributedSingleFlight` over the Redis or SQL Server lock. `InProcessSingleFlight` owns
a **private** locker instance rather than sharing the Memory backend's — the two nest, and
separate lockers make a key collision impossible.

### Namespace-Based Eviction

Cache keys include a namespace component. Namespaces can embed route parameter tokens (e.g., `"Account:{id}"`), so eviction can target a specific resource. Key construction is in `Common/Keys/ActionCacheKeyBuilder`.

### Cache Key Composition

A key has three components (`Common/Keys/ActionCacheKeyComponents`): route values, action arguments, and **vary-by values**. The third is resolved per request by `ActionCacheVaryByResolver` from the attribute's `VaryByUser` / `VaryByHeader` / `VaryByQuery` / `VaryByClaim` plus every registered `IActionCacheKeyContributor`.

`VaryByUserMode.Auto` is the default and varies by the authenticated user's identity — without it, two users on one `[Authorize]` endpoint share a cache entry and the second is served the first's response. The component is only serialized when non-empty, so keys for endpoints that vary by nothing are unchanged from before the feature existed.

Refresh **skips** entries whose `CachedResponse.VariesByRequest` is set — replaying another caller's request would mean impersonating them.

### Cancellation

Every `IActionCache` method takes a trailing `CancellationToken`; filters pass `HttpContext.RequestAborted`. `ResilientActionCache` rethrows `OperationCanceledException` when the *caller's* token is cancelled (even fail-open) but treats an elapsed `ActionCacheResilienceOptions.OperationTimeout` as a degradable backend failure. `IMemoryCache` is synchronous and StackExchange.Redis's `IDatabase` accepts no token, so those two check the token before dispatch; SQL Server and Cosmos forward it.

### Cached Values

Values are a `CachedResponse` (`Common/Responses/`): status code, content type, rendered body, plus the `CachedRequest` refresh replays. Non-polymorphic and serialized with System.Text.Json through a source-generated context — nothing in a payload names a type to construct. Bodies are rendered with the app's own `JsonSerializerOptions`.

### Refresh

`EndpointReplayRefreshProvider` re-issues a recorded request against the matching endpoint from `EndpointDataSource`, in its own DI scope, with a real `HttpContext`. `ActionCacheReplayMarker` marks that context and **every** cache filter checks it: the cache filters read through — otherwise a replay would be served the stale entry it exists to replace and write it straight back, making refresh a silent no-op — while the refresh and eviction filters skip. A refresh filter that re-entered would refresh from inside its own pass and never terminate; an eviction filter would clear the namespace the pass is warming.

`CachedResponseFactory.CreateRequest` returns `null` when a request cannot be faithfully
replayed — a body sent as XML or a form, which re-serialized JSON cannot stand in for. The
entry is still cached; refresh skips it and logs why. A JSON-compatible content type is
preserved as written (`+json` vendor types included) rather than flattened to
`application/json`, which used to have `[Consumes]` answer 415 on every pass.

Refresh works on Minimal API endpoints as well as controller actions (`WithActionCacheRefresh`); nothing in the replay is specific to either, since both dispatch through the endpoint's `RequestDelegate`.

### Attribute Validation

An endpoint either caches or has cache side effects, never both. `ActionCacheEndpointValidator`
walks `EndpointDataSource` at startup and throws `ConflictingCacheAttributesException` listing
every offending route. The rule itself is `ActionCacheDeclarationConflict.Detect`, a pure
function over `ActionCacheDeclaration` values so every combination is unit-testable without a
pipeline.

It is an `IStartupFilter`, not an `IHostedService`: endpoints do not exist until the request
pipeline is built, and a hosted service registered from `AddActionCache` starts *before* the
web host's own, seeing an empty endpoint collection. Validation runs after `next(app)`.

Two details that look like bugs but are not:
- MVC adds each action attribute to endpoint metadata **twice**, as the same instance.
  Declarations are de-duplicated by reference; counting naively fails every controller app.
- The Minimal API extensions capture their namespace in the filter closure rather than reading
  it back via `GetMetadata<T>()`, which returns the *last* match and made two chained calls
  both target the second namespace.

### Minimal API Endpoint Options

`WithActionCache(ns, options => ...)` configures expiration, vary-by and `SingleFlight` per
endpoint, matching `[ActionCache]`. `ActionCacheEndpointOptions` states expirations as
`TimeSpan` rather than the attribute's `long` milliseconds — an attribute argument must be a
compile-time constant, a builder argument need not be. The delegate runs **once at
registration**, not per request: the options describe the endpoint, so re-running caller code
on every request would only make an expensive lambda an expensive endpoint.

Eviction and refresh stay namespace-only, matching their attributes: neither writes an entry
the options would describe.

### Refresh and Expiration

`IActionCacheRefreshProvider.ReplayAsync` returns an `ActionCacheReplayResult`: the replayed
response plus the expirations the entry should be rewritten with, read from the endpoint the
recorded request resolved to. `ActionCacheBase` declares a **`protected abstract`** `SetAsync`
overload taking `ActionCacheEntryOptions?` so the refresh loop can write one entry with those.

That overload is deliberately not on `IActionCache`. The refresh loop calls it on `this`, and a
refresh never travels back out through `ActionCacheHandler` or `ResilientActionCache` — the
handler calls each link's `RefreshAsync` and the write happens inside that backend — so neither
wrapper needs to carry it and the public contract is unchanged. A caller wanting a cache with
particular expirations asks `IActionCacheFactory.Create(ns, absolute, sliding)` for one; only
refresh needs the value to vary per write.

Without this a refresh filter — which is created with no expirations — wrote replayed entries
through the *global* `UseEntryOptions`, discarding whatever the endpoint declared. One refresh
turned a bounded entry permanent. The value has to be per entry, not per cache: one namespace
can hold entries from several endpoints with different expirations.

The expirations are read from endpoint metadata rather than stored on `CachedResponse`, so the
serialized payload is unchanged for entries already in a backend and the attribute stays the
single source of truth. `WithActionCache` records its options on the metadata attribute for
exactly this reason.

### Layered Backends

`ActionCacheHandler` chains one cache per backend. `GetAsync` promotes a deeper-layer hit into the first layer; `GetKeysAsync` unions every layer (eviction and refresh depend on seeing all keys).

### DI Registration

```csharp
builder.Services.AddActionCache(options =>
{
    options.UseMemoryCache(/*...*/);
    options.UseRedisCache(/*...*/);
    // etc.
});
```

`AddActionCacheCommon` registers shared services and detects whether the app uses MVC or Minimal APIs to inject the appropriate descriptor and filter providers.

## Test Projects

- `test/Unit/` — NUnit + Moq; tests for key building, filter logic, and cache operations
- `test/Integration/` — `Microsoft.AspNetCore.TestHost`; full request pipeline tests with `UsersController` and `TeamsController` verifying cache hits, eviction, and refresh end-to-end

## Samples

`samples/Api/` and `samples/ApiWithExpiration/` are standalone ASP.NET Core apps that demonstrate library usage; they are not part of the test suite.

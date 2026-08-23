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
dotnet test test/Integration/Integration.csproj
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
| `IActionCacheRefreshProvider` | Re-invokes a cached action to refresh the cache |
| `IActionCacheDescriptorProvider` | Metadata about available controller/endpoint actions |

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

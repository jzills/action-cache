---
title: Configuration
weight: 1
---

Everything is configured through the builder passed to `AddActionCache`.

```csharp
builder.Services.AddActionCache(options =>
{
    options.UseMemoryCache(memory => memory.SizeLimit = 10_000);
    options.UseRedisCache("localhost:6379");

    options.UseEntryOptions(entry =>
    {
        entry.AbsoluteExpiration = TimeSpan.FromMinutes(5);
        entry.SlidingExpiration  = TimeSpan.FromMinutes(1);
    });

    options.UseOperationTimeout(TimeSpan.FromMilliseconds(250));
    options.UseDistributedSingleFlight();
});
```

## Builder methods

| Method | Purpose |
|---|---|
| `UseMemoryCache(Action<MemoryCacheOptions>)` | Register the in-process backend. |
| `UseRedisCache(Action<RedisCacheOptions>)` | Register Redis. |
| `UseRedisCache(string)` | Register Redis from a configuration string. |
| `UseSqlServerCache(Action<SqlServerCacheOptions>)` | Register SQL Server. |
| `UseAzureCosmosCache(Action<AzureCosmosCacheOptions>)` | Register Azure Cosmos DB. |
| `UseEntryOptions(Action<ActionCacheEntryOptions>)` | Default expirations and the key-index lock timeout. |
| `FailClosed(bool = true)` | Propagate backend failures instead of degrading. |
| `UseOperationTimeout(TimeSpan)` | Bound a single backend operation. |
| `UseDistributedSingleFlight(Action<ActionCacheSingleFlightOptions>?)` | Coalesce across instances. |
| `UseSingleFlightOptions(Action<ActionCacheSingleFlightOptions>)` | Tune coalescing without switching to distributed. |
| `UsePlaintextKeys()` | Emit readable, reversible keys. Debugging only. |
| `AddBackend(Action<IServiceCollection>)` | Register a backend of your own. |
| `AddDistributedLocker(...)` | Supply a distributed lock for a custom backend. |

Registration order is chain order — see [Layered backends](../../operations/layering).

## ActionCacheEntryOptions

Defaults for every entry, overridable per action on `[ActionCache]`.

| Property | Type | Default | Meaning |
|---|---|---|---|
| `AbsoluteExpiration` | `TimeSpan?` | `null` | Lifetime from when the entry is written. |
| `SlidingExpiration` | `TimeSpan?` | `null` | Idle lifetime, reset on each read. |
| `LockTimeout` | `TimeSpan` | 10s | How long to wait for a namespace **key-index** lock. |

{{< callout type="info" >}}
`LockTimeout` guards the short read-modify-write on a namespace's key index. It is **not**
the single-flight wait — that is `ActionCacheSingleFlightOptions.WaitTimeout`, which is held
across the origin action and sized differently on purpose.
{{< /callout >}}

## ActionCacheSingleFlightOptions

| Property | Type | Default | Meaning |
|---|---|---|---|
| `LeaseDuration` | `TimeSpan` | 30s | How long the leader may hold a key's lock before others assume it died. |
| `WaitTimeout` | `TimeSpan` | 10s | How long a caller waits before executing uncoalesced. |

Validated at startup: both must be positive, and `LeaseDuration` must exceed `WaitTimeout`.

Only locks with a time-to-live enforce the lease — that is Redis. See
[Stampede protection](../../reliability/stampede).

## ActionCacheResilienceOptions

Set through `FailClosed()` and `UseOperationTimeout()`.

| Property | Type | Default | Meaning |
|---|---|---|---|
| `FailClosed` | `bool` | `false` | Rethrow backend failures instead of degrading. |
| `OperationTimeout` | `TimeSpan?` | `null` | Abandon a backend operation after this long. |

See [Resilience](../../reliability/resilience).

## ActionCache attribute properties

| Property | Type | Default |
|---|---|---|
| `Namespace` | `string` | *required* |
| `AbsoluteExpiration` | `long` (ms) | `0` — none |
| `SlidingExpiration` | `long` (ms) | `0` — none |
| `VaryByUser` | `VaryByUserMode` | `Auto` |
| `VaryByHeader` | `string?` | `null` |
| `VaryByQuery` | `string?` | `null` |
| `VaryByClaim` | `string?` | `null` |
| `SingleFlight` | `bool` | `true` |

`[ActionCacheEviction]` and `[ActionCacheRefresh]` take `Namespace` only.

## Key contributors

```csharp
builder.Services.AddActionCacheKeyContributor<TenantKeyContributor>();
```

See [Vary-by](../../caching/vary-by).

## Diagnostics names

| Constant | Value |
|---|---|
| `ActionCacheDiagnostics.MeterName` | `ActionCache` |
| `ActionCacheDiagnostics.ActivitySourceName` | `ActionCache` |

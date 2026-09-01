# ActionCache.Redis

The Redis backend for [ActionCache](https://www.nuget.org/packages/ActionCache/) —
namespaced response caching for ASP.NET Core.

```bash
dotnet add package ActionCache.Redis
```

This package references `ActionCache`, so installing it is enough — you do not need the
core package as well.

## Registration

```csharp
using ActionCache.Common.Extensions;

builder.Services.AddActionCache(options =>
{
    options.UseRedisCache(redis => redis.Configuration = "localhost:6379");
});
```

The delegate configures `RedisCacheOptions`, so a `ConfigurationOptions` instance,
credentials or TLS settings all go here. A shorthand overload takes the configuration
string directly:

```csharp
options.UseRedisCache("localhost:6379");
```

## Keyspace notifications

The backend keeps a sorted-set index per namespace, which is what makes namespace eviction
and refresh possible without scanning. Entries that expire on their own are removed from
that index in response to Redis key-expired events, so enable the `Ex` flags:

```bash
redis-cli config set notify-keyspace-events Ex
```

Without them nothing breaks — the index self-heals lazily when it is next read. Enabling
the flags keeps it tight instead.

## Distributed single-flight

This backend supplies a distributed lock, so it can back `options.UseDistributedSingleFlight()`
to coalesce concurrent cache misses across processes rather than only within one.

## Documentation

Full documentation: <https://jzills.github.io/action-cache/docs/backends/redis/>

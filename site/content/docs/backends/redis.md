---
title: Redis
weight: 2
---

```bash
dotnet add package ActionCache.Redis
```

```csharp
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

The Redis backend keeps a sorted-set index per namespace, which is what makes namespace
eviction and refresh possible without scanning. Entries that expire on their own have to be
removed from that index, and the backend learns about them from Redis key-expired events.

Enable the `Ex` flags for that cleanup to run:

```bash
redis-cli config set notify-keyspace-events Ex
```

or in `redis.conf`:

```
notify-keyspace-events Ex
```

The listener targets the database in your connection string.

{{< callout type="info" >}}
Without `Ex` nothing breaks. The index self-heals lazily when it is next read — cleanup is
deferred rather than lost. Enabling the flags keeps the index tight instead.
{{< /callout >}}

If the subscription cannot be established at startup — Redis not up yet, for instance — the
host does not fail. The failure is logged at `Warning` and retried with exponential backoff
until it succeeds.

## Locking

None needed for cache operations: they are atomic through Lua scripts.

Redis **does** supply a distributed lock, which is what
[`UseDistributedSingleFlight()`](../../reliability/stampede) uses. When both Redis and SQL
Server are registered, Redis is preferred.

## Connection

The connection is established lazily, so an application starts even when Redis is not
reachable yet. Until it is, cache operations degrade under the usual
[resilience](../../reliability/resilience) rules.

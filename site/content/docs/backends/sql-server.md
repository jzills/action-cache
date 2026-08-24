---
title: SQL Server
weight: 3
---

```bash
dotnet add package ActionCache.SqlServer
```

```csharp
builder.Services.AddActionCache(options =>
{
    options.UseSqlServerCache(sql =>
    {
        sql.ConnectionString = configuration.GetConnectionString("Cache");
        sql.SchemaName = "dbo";
        sql.TableName = "DistributedCache";
    });
});
```

The delegate configures `SqlServerCacheOptions`, the same type
`Microsoft.Extensions.Caching.SqlServer` uses.

## The table

This backend stores entries in the standard distributed-cache table, which you create with
the `dotnet-sql-cache` tool:

```bash
dotnet tool install --global dotnet-sql-cache
dotnet sql-cache create "<connection string>" dbo DistributedCache
```

## Locking

`sp_getapplock` / `sp_releaseapplock`, session-scoped. That is also what backs
[`UseDistributedSingleFlight()`](../../reliability/stampede) when Redis is not registered.

A session-scoped lock cannot expire while held, and a process that dies releases its locks
by disconnecting — so the lease duration in `ActionCacheSingleFlightOptions` has nothing to
enforce here. It applies to Redis, where the lock is a key with a TTL.

## Cancellation

The SQL Server backend forwards the `CancellationToken` to the underlying commands, so a
client that disconnects stops work in flight rather than letting it run to completion.

## When to use it

A stack that already runs SQL Server and would rather not add Redis. It is slower than
Redis for cache traffic, but it is one less thing to operate.

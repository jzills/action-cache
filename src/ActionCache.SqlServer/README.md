# ActionCache.SqlServer

The SQL Server backend for [ActionCache](https://www.nuget.org/packages/ActionCache/) —
namespaced response caching for ASP.NET Core.

```bash
dotnet add package ActionCache.SqlServer
```

This package references `ActionCache`, so installing it is enough — you do not need the
core package as well.

## Registration

```csharp
using ActionCache.Common.Extensions;

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

Entries are stored in the standard distributed-cache table, which you create with the
`dotnet-sql-cache` tool:

```bash
dotnet tool install --global dotnet-sql-cache
dotnet sql-cache create "<connection string>" dbo DistributedCache
```

## Distributed single-flight

This backend supplies a distributed lock, built on session-scoped `sp_getapplock` /
`sp_releaseapplock`, so it can back `options.UseDistributedSingleFlight()` to coalesce
concurrent cache misses across processes rather than only within one.

## Documentation

Full documentation: <https://jzills.github.io/action-cache/docs/backends/sql-server/>

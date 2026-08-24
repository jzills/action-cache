---
title: Installation
weight: 1
---

## Packages

ActionCache ships as a core package plus one package per distributed backend. The core
package carries the attributes, the filters and the in-memory backend, and takes no
dependency on Redis, SQL Server or Cosmos — installing it does not drag in a driver you
are not using.

| Package | Contents |
|---|---|
| `ActionCache` | Attributes, filters, key building, DI, and the **Memory** backend |
| `ActionCache.Redis` | The Redis backend and its distributed lock |
| `ActionCache.SqlServer` | The SQL Server backend and its distributed lock |
| `ActionCache.AzureCosmos` | The Azure Cosmos DB backend |
| `ActionCache.Abstractions` | `IActionCache` and friends, for writing a backend of your own |

Every backend package references `ActionCache`, so installing one is enough:

```bash
dotnet add package ActionCache.Redis
```

The backend packages are pinned to the exact matching version of `ActionCache`. They share
internals, so a mismatched pair can fail at runtime rather than at build time — the exact
version range makes that lockstep explicit rather than assumed.

## Target frameworks

`net8.0` and `net10.0`.

## Registration

One call, with one `Use…Cache` per backend:

```csharp
using ActionCache.Common.Extensions;

builder.Services.AddActionCache(options =>
{
    options.UseRedisCache(redis => redis.Configuration = "localhost:6379");
});
```

The `Use…Cache` extensions are declared in `ActionCache.Common.Extensions` — the namespace
you already import for `AddActionCache` — so adding a backend package does not mean adding
a `using`.

`AddActionCache` detects whether the application uses MVC or Minimal APIs and registers the
matching filter and descriptor providers, so nothing further is needed for either style.

## Next

{{< cards >}}
  {{< card link="quickstart" title="Quickstart" subtitle="Cache, evict and refresh an endpoint." >}}
  {{< card link="../backends" title="Backends" subtitle="Configuration for each store." >}}
{{< /cards >}}

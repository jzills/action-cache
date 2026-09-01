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

## Upgrading from 0.0.9

`0.0.9` shipped as a single package containing every backend. `0.1.0` is the first release
split across several, so an existing application needs the package for each backend it
registers:

```bash
dotnet add package ActionCache.Redis        # if you call UseRedisCache
dotnet add package ActionCache.SqlServer    # if you call UseSqlServerCache
dotnet add package ActionCache.AzureCosmos  # if you call UseAzureCosmosCache
```

**No code changes.** The `Use…Cache` extensions still live in
`ActionCache.Common.Extensions`, so no call site and no `using` moves. `ActionCache` on its
own still covers `UseMemoryCache`.

Three things to know before deploying:

- **`net9.0` is no longer targeted.** `0.0.9` shipped `net8.0` and `net9.0`; this release
  ships `net8.0` and `net10.0`.
- **Expect a cold cache.** Both the key format and the stored payload changed, so entries
  left in a distributed backend by `0.0.9` are ignored and rewritten as they are re-cached.
  A drop in hit rate on first deploy is expected; nothing errors.
- **Responses now vary by the authenticated user by default.** Two users on one
  `[Authorize]` endpoint no longer share an entry — see [Vary-by](../../caching/vary-by).
  Set `VaryByUser = VaryByUserMode.Never` to keep one shared entry.

The [changelog](https://github.com/jzills/action-cache/blob/main/CHANGELOG.md) lists every
breaking change.

## Next

{{< cards >}}
  {{< card link="quickstart" title="Quickstart" subtitle="Cache, evict and refresh an endpoint." >}}
  {{< card link="../backends" title="Backends" subtitle="Configuration for each store." >}}
{{< /cards >}}

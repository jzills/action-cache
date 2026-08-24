<div align="center">

<img src="resources/banner.svg" alt="ActionCache" width="640" />

**Namespaced response caching for ASP.NET Core — Memory, Redis, SQL Server, and Azure Cosmos DB.**

[![CI](https://img.shields.io/github/actions/workflow/status/jzills/action-cache/pr.yml?style=flat-square&label=CI&labelColor=21262d)](https://github.com/jzills/action-cache/actions/workflows/pr.yml)
[![CodeQL](https://img.shields.io/github/actions/workflow/status/jzills/action-cache/codeql.yml?style=flat-square&label=CodeQL&labelColor=21262d)](https://github.com/jzills/action-cache/actions/workflows/codeql.yml)
[![NuGet](https://img.shields.io/nuget/v/ActionCache?style=flat-square&label=NuGet&color=a371f7&labelColor=21262d)](https://www.nuget.org/packages/ActionCache/)
[![Downloads](https://img.shields.io/nuget/dt/ActionCache?style=flat-square&label=downloads&color=a371f7&labelColor=21262d)](https://www.nuget.org/packages/ActionCache/)
[![License: MIT](https://img.shields.io/badge/license-MIT-a371f7?style=flat-square&labelColor=21262d)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-a371f7?style=flat-square&labelColor=21262d)](https://dotnet.microsoft.com/)
[![Docs](https://img.shields.io/badge/docs-jzills.github.io-a371f7?style=flat-square&labelColor=21262d)](https://jzills.github.io/action-cache/)

</div>

- [Summary](#summary)
- [Features](#features)
- [Installation](#installation)
- [Quickstart](#quickstart)
- [Documentation](https://jzills.github.io/action-cache/)
- [Resources](#resources)

## Summary

ActionCache adds a caching layer to ASP.NET Core by annotating your endpoints. Cache a
response, evict entries by namespace, or refresh cached actions with a single attribute —
against an in-process or distributed backend, or several layered together.

## Why not `[OutputCache]` or `HybridCache`?

Fair question, and worth answering before you install anything.

| | `[OutputCache]` | `HybridCache` | **ActionCache** |
|---|---|---|---|
| Ships with ASP.NET Core | Yes | Yes (.NET 9+) | No — a package |
| Caches | Full HTTP response | Arbitrary values you fetch yourself | Action/endpoint responses |
| Attribute-driven | Yes | No — you call it in code | Yes |
| Stampede protection | No | Yes | Yes |
| L1 + L2 layering | No | Yes (memory + `IDistributedCache`) | Yes, and any number of layers |
| Backends | Memory, `IDistributedCache` | Memory + `IDistributedCache` | Memory, Redis, **SQL Server**, **Cosmos DB** |
| Invalidate a group | Tags | Tags | Namespaces, with **route parameters** (`Account:{id}`) |
| Vary by caller | `VaryByValue` (manual) | Your key, your problem | **Automatic** for authenticated requests |
| Re-warm entries | No | No | Yes — replays the recorded request |

**Use `[OutputCache]`** if it covers you. It is built in, costs no dependency, and needs no
explanation to the next person who reads your code.

**Use `HybridCache`** if you are caching values rather than responses, and want stampede
protection and two-tier caching from the framework.

**Reach for ActionCache** when you want attribute-driven response caching *and* something
the other two don't do: eviction scoped to a route parameter, SQL Server or Cosmos as a
backend, warming entries ahead of expiry, or per-user keys you don't have to remember to
ask for.

## Features

- **Attribute-driven** — add caching, eviction, or refresh to any endpoint with one
  attribute. Works with both **MVC controllers** and **Minimal APIs**.
- **Four backends** — in-process **Memory**, **Redis**, **SQL Server**, and
  **Azure Cosmos DB**, used individually or layered together.
- **Namespaced eviction** — group entries under a namespace (with route-parameter
  templates like `Account:{id}`) and evict a whole namespace in one call.
- **Cache refresh** — replay the request recorded on each entry to warm it ahead of expiry.
- **Fail-open by default** — a backend outage degrades to a cache miss and logs a
  warning so requests still succeed; opt into fail-closed to propagate errors instead.

## Installation

ActionCache ships as one package per backend, so you only take the dependencies you use:

```bash
dotnet add package ActionCache                 # core + in-memory caching
dotnet add package ActionCache.Redis           # add for Redis
dotnet add package ActionCache.SqlServer       # add for SQL Server
dotnet add package ActionCache.AzureCosmos     # add for Azure Cosmos DB
```

| Package | Depends on |
|---------|------------|
| [`ActionCache`](https://www.nuget.org/packages/ActionCache/) | `ActionCache.Abstractions` only — no Redis, SqlClient, Cosmos SDK or Newtonsoft |
| [`ActionCache.Redis`](https://www.nuget.org/packages/ActionCache.Redis/) | StackExchange.Redis |
| [`ActionCache.SqlServer`](https://www.nuget.org/packages/ActionCache.SqlServer/) | Microsoft.Data.SqlClient |
| [`ActionCache.AzureCosmos`](https://www.nuget.org/packages/ActionCache.AzureCosmos/) | Microsoft.Azure.Cosmos, Newtonsoft.Json |
| [`ActionCache.Abstractions`](https://www.nuget.org/packages/ActionCache.Abstractions/) | nothing — reference it to write a custom backend |

Configuration is unchanged: `options.UseRedisCache(...)` and friends still read the same,
they just live in their own package now.

Targets **.NET 8** and **.NET 10**.

## Quickstart

Register a cache backend:

```csharp
using ActionCache.Common.Extensions;

builder.Services.AddActionCache(options =>
{
    options.UseMemoryCache(memory => { });
    // or UseRedisCache(...), UseSqlServerCache(...), UseAzureCosmosCache(...)
});
```

Then annotate your endpoints — cache a read, evict on write:

```csharp
using ActionCache.Attributes;

[HttpGet("forecasts")]
[ActionCache(Namespace = "Forecasts")]
public IActionResult Get() => Ok(_forecasts);

[HttpPost("forecasts")]
[ActionCacheEviction(Namespace = "Forecasts")]
public IActionResult Create(Forecast forecast) => Ok(_repository.Add(forecast));
```

See the [documentation site](https://jzills.github.io/action-cache/) for expiration,
route-templated namespaces, layered backends, refresh, vary-by, resilience and
observability.

## Resources

- [Documentation](https://jzills.github.io/action-cache/)
- [Samples](./samples/)

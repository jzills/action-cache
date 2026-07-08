<div align="center">

<img src="resources/banner.svg" alt="ActionCache" width="640" />

**Namespaced response caching for ASP.NET Core — Memory, Redis, SQL Server, and Azure Cosmos DB.**

[![CI](https://img.shields.io/github/actions/workflow/status/jzills/action-cache/pr.yml?style=flat-square&label=CI&labelColor=21262d)](https://github.com/jzills/action-cache/actions/workflows/pr.yml)
[![CodeQL](https://img.shields.io/github/actions/workflow/status/jzills/action-cache/codeql.yml?style=flat-square&label=CodeQL&labelColor=21262d)](https://github.com/jzills/action-cache/actions/workflows/codeql.yml)
[![NuGet](https://img.shields.io/nuget/v/ActionCache?style=flat-square&label=NuGet&color=a371f7&labelColor=21262d)](https://www.nuget.org/packages/ActionCache/)
[![Downloads](https://img.shields.io/nuget/dt/ActionCache?style=flat-square&label=downloads&color=a371f7&labelColor=21262d)](https://www.nuget.org/packages/ActionCache/)
[![License: MIT](https://img.shields.io/badge/license-MIT-a371f7?style=flat-square&labelColor=21262d)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-a371f7?style=flat-square&labelColor=21262d)](https://dotnet.microsoft.com/)

</div>

- [Summary](#summary)
- [Features](#features)
- [Installation](#installation)
- [Quickstart](#quickstart)
- [Documentation](./src/README.md)
- [Resources](#resources)

## Summary

ActionCache adds a caching layer to ASP.NET Core by annotating your endpoints. Cache a
response, evict entries by namespace, or refresh cached actions with a single attribute —
against an in-process or distributed backend, or several layered together.

## Features

- **Attribute-driven** — add caching, eviction, or refresh to any endpoint with one
  attribute. Works with both **MVC controllers** and **Minimal APIs**.
- **Four backends** — in-process **Memory**, **Redis**, **SQL Server**, and
  **Azure Cosmos DB**, used individually or layered together.
- **Namespaced eviction** — group entries under a namespace (with route-parameter
  templates like `Account:{id}`) and evict a whole namespace in one call.
- **Cache refresh** — re-invoke cached actions to warm entries ahead of expiry.
- **Fail-open by default** — a backend outage degrades to a cache miss and logs a
  warning so requests still succeed; opt into fail-closed to propagate errors instead.

## Installation

`ActionCache` is available on [NuGet](https://www.nuget.org/packages/ActionCache/):

```bash
dotnet add package ActionCache
```

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

See the [full documentation](./src/README.md) for expiration, route-templated
namespaces, multiple backends, cache refresh, and resilience options.

## Resources

- [Documentation](./src/README.md)
- [Samples](./samples/)

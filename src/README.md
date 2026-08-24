
# ActionCache

[![NuGet Version](https://img.shields.io/nuget/v/ActionCache.svg)](https://www.nuget.org/packages/ActionCache/) [![NuGet Downloads](https://img.shields.io/nuget/dt/ActionCache.svg)](https://www.nuget.org/packages/ActionCache/)

Namespaced response caching for ASP.NET Core — Memory, Redis, SQL Server, and Azure Cosmos DB.

**📖 Full documentation: <https://jzills.github.io/action-cache/>**

## Install

```bash
dotnet add package ActionCache                 # core + in-memory caching
dotnet add package ActionCache.Redis           # add for Redis
dotnet add package ActionCache.SqlServer       # add for SQL Server
dotnet add package ActionCache.AzureCosmos     # add for Azure Cosmos DB
```

Targets **.NET 8** and **.NET 10**.

## Quickstart

Register a backend:

```csharp
using ActionCache.Common.Extensions;

builder.Services.AddActionCache(options =>
{
    options.UseMemoryCache(memory => memory.SizeLimit = 10_000);
});
```

Cache a read, evict on write:

```csharp
using ActionCache.Attributes;

[HttpGet("forecasts")]
[ActionCache(Namespace = "Forecasts")]
public IActionResult Get() => Ok(_repository.All());

[HttpPost("forecasts")]
[ActionCacheEviction(Namespace = "Forecasts")]
public IActionResult Create(Forecast forecast) => Ok(_repository.Add(forecast));
```

Entries are grouped under a **namespace**, which is what makes eviction and refresh
possible without tracking keys. A namespace can embed route tokens — `Account:{id}` gives
every account its own group.

## What you get without asking

- **Per-user keys** on authenticated endpoints, so one caller is never served another's response.
- **Stampede protection** — concurrent misses for one key are coalesced and the action runs once.
- **Fail-open** — a backend outage degrades to a cache miss and the request still succeeds.
- **Hashed keys** — SHA-256, so nothing readable is left in the store.

## Documentation

| | |
|---|---|
| [Getting started](https://jzills.github.io/action-cache/docs/getting-started/installation/) | Packages and registration |
| [Backends](https://jzills.github.io/action-cache/docs/backends/) | Memory, Redis, SQL Server, Cosmos |
| [Attributes](https://jzills.github.io/action-cache/docs/caching/attributes/) | Every option on the three attributes |
| [Vary-by](https://jzills.github.io/action-cache/docs/caching/vary-by/) | Who a cached response belongs to |
| [Eviction](https://jzills.github.io/action-cache/docs/operations/eviction/) · [Refresh](https://jzills.github.io/action-cache/docs/operations/refresh/) · [Layering](https://jzills.github.io/action-cache/docs/operations/layering/) | Operations |
| [Resilience](https://jzills.github.io/action-cache/docs/reliability/resilience/) · [Stampede](https://jzills.github.io/action-cache/docs/reliability/stampede/) | Reliability |
| [Observability](https://jzills.github.io/action-cache/docs/observability/) | Metrics and traces |
| [Configuration reference](https://jzills.github.io/action-cache/docs/reference/configuration/) | Every option in one table |

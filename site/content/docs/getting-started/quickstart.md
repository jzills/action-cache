---
title: Quickstart
weight: 2
---

A worked example: cache a read, evict it on write, and warm it again afterwards.

## 1. Register a backend

```csharp
using ActionCache.Common.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddActionCache(options =>
{
    options.UseMemoryCache(memory => memory.SizeLimit = 10_000);
});

var app = builder.Build();
app.MapControllers();
app.Run();
```

## 2. Cache a response

`Namespace` is required. It is the group the entry belongs to, and the handle eviction and
refresh use later.

```csharp
[ApiController]
[Route("forecasts")]
public class ForecastsController : ControllerBase
{
    [HttpGet]
    [ActionCache(Namespace = "Forecasts")]
    public IActionResult Get() => Ok(_repository.All());
}
```

The second identical request is served from the cache without the action running.

## 3. Evict when the data changes

```csharp
[HttpPost]
[ActionCacheEviction(Namespace = "Forecasts")]
public IActionResult Create(Forecast forecast) => Ok(_repository.Add(forecast));
```

Eviction runs **after a successful response** and removes every entry in the namespace —
you never name a key.

## 4. Or refresh instead of evicting

Eviction leaves the next reader to pay for a cold cache. Refresh re-populates the namespace
instead, by replaying the request recorded on each entry:

```csharp
[HttpPost]
[ActionCacheRefresh(Namespace = "Forecasts")]
public IActionResult Create(Forecast forecast) => Ok(_repository.Add(forecast));
```

## Minimal APIs

Endpoints use builder extensions rather than attributes:

```csharp
using ActionCache.EndpointFilters.Extensions;

app.MapGet("/forecasts", () => repository.All())
   .WithActionCache("Forecasts");

app.MapDelete("/forecasts/{id}", (int id) => repository.Remove(id))
   .WithActionCacheEviction("Forecasts");

app.MapPost("/forecasts", (Forecast forecast) => repository.Add(forecast))
   .WithActionCacheRefresh("Forecasts");
```

{{< callout type="warning" >}}
The Minimal API surface is narrower than the MVC one. Each extension takes a namespace and
nothing else, so expiration, vary-by and `SingleFlight` cannot be set per endpoint — they
fall back to the defaults and to whatever `UseEntryOptions` configures globally.
{{< /callout >}}

## What you get without asking

- **Per-user keys.** On an `[Authorize]` endpoint the caller's identity joins the key, so
  two users cannot be served each other's response. See [Vary-by](../caching/vary-by).
- **Stampede protection.** Concurrent misses for one key are coalesced and the action runs
  once. See [Stampede protection](../reliability/stampede).
- **Fail-open.** A backend that throws degrades to a miss and the request still succeeds.
  See [Resilience](../reliability/resilience).

## Next

{{< cards >}}
  {{< card link="../caching/attributes" title="Attributes" subtitle="Every option on the three attributes." >}}
  {{< card link="../backends" title="Backends" subtitle="Configure Redis, SQL Server or Cosmos." >}}
{{< /cards >}}

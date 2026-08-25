---
title: Attributes
weight: 1
---

Three attributes drive everything. Each takes a required `Namespace`; only `[ActionCache]`
takes anything else.

## ActionCache

Caches the action's response.

```csharp
[HttpGet]
[ActionCache(Namespace = "Forecasts")]
public IActionResult Get() => Ok(_repository.All());
```

| Property | Type | Default | Meaning |
|---|---|---|---|
| `Namespace` | `string` | *required* | The group the entry belongs to. May embed route tokens — see [below](#route-templates-in-a-namespace). |
| `AbsoluteExpiration` | `long` | `0` (none) | Lifetime in **milliseconds** from when the entry is written. |
| `SlidingExpiration` | `long` | `0` (none) | Idle lifetime in **milliseconds**, reset on each read. |
| `VaryByUser` | `VaryByUserMode` | `Auto` | Whether the caller's identity joins the key. See [Vary-by](../vary-by). |
| `VaryByHeader` | `string?` | `null` | Comma-separated header names. |
| `VaryByQuery` | `string?` | `null` | Comma-separated query keys. |
| `VaryByClaim` | `string?` | `null` | Comma-separated claim types. |
| `SingleFlight` | `bool` | `true` | Coalesce concurrent misses. See [Stampede protection](../../reliability/stampede). |

{{< callout type="info" >}}
The two expirations on the attribute are `long` milliseconds because attribute arguments
must be compile-time constants — `TimeSpan` cannot appear there. The equivalents on
`UseEntryOptions` are `TimeSpan`.
{{< /callout >}}

Only responses that represent success are stored. An action that returns a `4xx` or `5xx`
result leaves the cache untouched, so an outage cannot be cached and then served back after
it has passed.

## ActionCacheEviction

Drops every entry in the namespace, after a successful response.

```csharp
[HttpDelete("{id}")]
[ActionCacheEviction(Namespace = "Forecasts")]
public IActionResult Delete(int id) => Ok(_repository.Remove(id));
```

Several namespaces can be evicted at once, comma-separated:

```csharp
[ActionCacheEviction(Namespace = "Forecasts, Summaries")]
```

See [Eviction](../../operations/eviction).

## ActionCacheRefresh

Re-populates the namespace instead of emptying it, by replaying the request recorded on
each entry.

```csharp
[HttpPost]
[ActionCacheRefresh(Namespace = "Forecasts")]
public IActionResult Create(Forecast forecast) => Ok(_repository.Add(forecast));
```

See [Refresh](../../operations/refresh).

## Route templates in a namespace

A namespace can embed route parameters, which makes the group per-resource:

```csharp
[HttpGet("{id}")]
[ActionCache(Namespace = "Account:{id}")]
public IActionResult Get(Guid id, DateTime offset) => Ok(_repository.For(id, offset));
```

Every account then has its own namespace, and the various `offset` values for one account
live inside it. Eviction and refresh can then target a single account:

```csharp
[HttpPut("{id}")]
[ActionCacheEviction(Namespace = "Account:{id}")]
public IActionResult Update(Guid id, AccountModel model) => Ok(_repository.Save(id, model));
```

{{< callout type="info" >}}
The resolved namespace (`Account:42`) is what groups entries in the store. The **template**
(`Account:{id}`) is what appears in metrics and traces, because the resolved form is one
time series per account. See [Observability](../../observability).
{{< /callout >}}

## Minimal APIs

Endpoints use builder extensions:

```csharp
using ActionCache.EndpointFilters.Extensions;

app.MapGet("/forecasts", () => repository.All()).WithActionCache("Forecasts");
app.MapDelete("/forecasts", () => repository.Clear()).WithActionCacheEviction("Forecasts");
app.MapPost("/forecasts", (Forecast f) => repository.Add(f)).WithActionCacheRefresh("Forecasts");
```

Each takes a namespace and nothing else — the per-endpoint options in the table above are
MVC-only.

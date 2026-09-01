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

`WithActionCache` takes the same per-endpoint options as the attribute, through a configure
delegate:

```csharp
app.MapGet("/forecasts", () => repository.All())
   .WithActionCache("Forecasts", options =>
   {
       options.AbsoluteExpiration = TimeSpan.FromMinutes(5);
       options.VaryByQuery = "page,size";
       options.SingleFlight = false;
   });
```

Expirations are a `TimeSpan` rather than the milliseconds the attribute takes. That difference
is not gratuitous: an attribute argument has to be a compile-time constant, so `[ActionCache]`
cannot hold a `TimeSpan` and states its expirations as `long` instead. A builder has no such
constraint.

`WithActionCacheEviction` and `WithActionCacheRefresh` still take a namespace and nothing else,
matching their attributes — neither writes a cache entry, so there is no entry for expiration
or vary-by to describe.

## Combining attributes on one endpoint

An endpoint either **caches**, or has **cache side effects** — never both. The rules are
checked when the host starts, and a violation throws
`ConflictingCacheAttributesException` naming every offending route.

| Combination | Allowed |
|---|---|
| `[ActionCache]` alone | Yes |
| Several evictions or refreshes, **different** namespaces | Yes |
| Eviction and refresh together, **different** namespaces | Yes |
| `[ActionCache]` with eviction or refresh, any namespace | **No** |
| Two `[ActionCache]` | **No** |
| Two side effects naming the **same** namespace | **No** |

Caching alongside a side effect is rejected even when the namespaces differ, and the reason is
not tidiness. The eviction and refresh filters run *inside* the cache filter, so a cached
response never reaches the endpoint and the side effect never runs:

```
miss → cached, evicted    ✓
hit  → served from cache, nothing evicted
```

It behaves correctly against a cold cache and silently stops as soon as the cache warms up —
which is to say, correctly in development and wrongly in production. Put the side effect on the
endpoint that performs the write instead.

Two side effects on one namespace are rejected because they contradict each other: refresh
warms the namespace, eviction empties it, and which one wins depends on the order the
attributes happen to be written in.

---
title: Eviction
weight: 1
---

Eviction removes **every entry in a namespace**. You never name a key — that is the point
of grouping entries under a namespace in the first place.

```csharp
[HttpDelete("{id}")]
[ActionCacheEviction(Namespace = "Forecasts")]
public IActionResult Delete(int id) => Ok(_repository.Remove(id));
```

## When it runs

**After** the action, and only when the response was a success. An action that fails leaves
the cache alone, so a failed write cannot throw away a cache that still matches the data.

## Several namespaces at once

```csharp
[ActionCacheEviction(Namespace = "Forecasts, Summaries")]
```

Both groups are evicted on one successful response.

## Per-resource eviction

With a route token in the namespace, eviction targets a single resource:

```csharp
[HttpPut("{id}")]
[ActionCacheEviction(Namespace = "Account:{id}")]
public IActionResult Update(Guid id, AccountModel model) => Ok(_repository.Save(id, model));
```

Updating account 42 clears `Account:42` and leaves every other account's entries in place.

## Across layers

When several backends are registered, eviction reaches all of them. Key enumeration unions
every layer, so an entry that exists only in the deepest store is still removed.

## Minimal APIs

```csharp
app.MapDelete("/forecasts", () => repository.Clear())
   .WithActionCacheEviction("Forecasts");
```

## Eviction during a refresh replay

Eviction is skipped on a [refresh](../refresh) replay. An endpoint that carries both
eviction and caching is replayed like any other, and evicting there would clear the very
namespace the refresh pass is in the middle of warming — refresh would leave the cache
emptier than it found it. Ordinary requests to that endpoint evict as normal.

## Eviction or refresh?

Eviction is cheap and leaves the next reader to repopulate. [Refresh](../refresh) costs more
at write time and leaves the cache warm. Use eviction when reads are infrequent enough that
a cold entry does not matter, or when the endpoint's entries vary by request — which refresh
skips anyway.

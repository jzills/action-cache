---
title: Refresh
weight: 2
---

Refresh re-populates a namespace's entries with current data, instead of emptying it. It
works by **replaying the request that produced each entry**.

```csharp
[HttpGet("forecasts")]
[ActionCache(Namespace = "Forecasts")]
public IActionResult Get() => Ok(_repository.All());

[HttpPost("forecasts")]
[ActionCacheRefresh(Namespace = "Forecasts")]
public IActionResult Create(Forecast forecast) => Ok(_repository.Add(forecast));
```

After a forecast is created, every cached entry under `Forecasts` is re-issued and rewritten
with the result, so the next reader gets fresh data without paying for a miss.

## How replay works

The method, path, query string and — where there is one — the request body are recorded on
the entry alongside the response. Refresh re-issues them against the matching endpoint from
`EndpointDataSource`, with a real `HttpContext`, in its **own DI scope**, so model binding,
action filters, the action and result execution all run normally and nothing disturbs the
request that triggered the refresh.

The replayed context is marked so the cache filters bypass the cache for it. Without that,
a replay would be served the stale entry it exists to replace and write it straight back,
making refresh a silent no-op.

## What replay does not run

It executes the **endpoint**, not the surrounding pipeline. Outer middleware —
authentication, CORS, exception handling — does not run, because that belongs to the request
pipeline rather than to the endpoint.

## What gets skipped

Two kinds of entry are skipped, each logged:

**Entries that vary by request context.** Replaying one would mean re-issuing another
caller's request in order to warm their entry. Since
[`VaryByUserMode.Auto`](../../caching/vary-by) is the default, this is every authenticated
endpoint. These are kept fresh by ordinary expiry instead.

**Entries with no recorded request**, which only happens for entries written by an older
version of the library.

A single entry that fails to replay does not abort the pass — it is logged and the
remaining entries are still refreshed.

## What is recorded, and what is not

Only the request line and, where applicable, the body. **Never headers** — they routinely
carry credentials, and a cache entry is not a safe place to keep them.

A body is recorded only for entries refresh could actually replay, so an entry that varies
by request stores no payload at all.

{{< callout type="warning" >}}
The recorded body is re-serialized as JSON and replayed with `application/json`. An endpoint
that consumes a different media type — `application/xml`, or a vendor type such as
`application/vnd.example.v1+json` — will be answered `415` on replay, and refresh for that
namespace becomes a no-op that logs each pass. Use [eviction](../eviction) for those
endpoints.
{{< /callout >}}

## Minimal APIs

Refresh is currently **MVC-only**. There is no `WithActionCacheRefresh` extension for
endpoints; use [eviction](../eviction) there instead.

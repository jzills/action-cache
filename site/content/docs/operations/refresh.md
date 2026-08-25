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

The recorded body is re-serialized as JSON, so the content type it is replayed with has to be
one the endpoint accepts JSON for. The request's own content type is preserved when it is
JSON-compatible — `application/json`, `text/json`, and any type with a `+json` suffix, which
covers versioned APIs declaring something like `application/vnd.example.v1+json`.

{{< callout type="warning" >}}
A request whose body was **not** JSON — `application/xml`, a form post — cannot be replayed,
because no content type makes re-serialized JSON bind to it. Those entries are cached but not
refreshable: refresh skips them and logs the skip once per pass, rather than replaying into a
`415` that would replace a working entry with an error. Use [eviction](../eviction) for those
endpoints.
{{< /callout >}}

## Minimal APIs

Endpoints refresh through a builder extension, and behave identically:

```csharp
using ActionCache.EndpointFilters.Extensions;

app.MapGet("/forecasts", () => repository.All())
   .WithActionCache("Forecasts");

app.MapPost("/forecasts", (Forecast forecast) => repository.Add(forecast))
   .WithActionCacheRefresh("Forecasts");
```

Nothing about the replay is specific to either hosting model: the recorded request is
resolved against `EndpointDataSource` and dispatched through the endpoint's own
`RequestDelegate`, which is how a controller action is invoked too. The
`VariesByRequest` skip, the media-type limitation above, and the `Cache-Status: Refresh`
header all apply unchanged.

`WithActionCacheRefresh` takes a namespace and nothing else, in common with the rest of the
Minimal API surface — see [the attributes reference](../../caching/attributes#minimal-apis).

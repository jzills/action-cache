---
title: Observability
weight: 6
---

ActionCache publishes a `Meter` and an `ActivitySource`, both named `ActionCache`. Neither
does anything until something subscribes, so there is no flag to turn them on:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter(ActionCacheDiagnostics.MeterName))
    .WithTracing(tracing => tracing.AddSource(ActionCacheDiagnostics.ActivitySourceName));
```

## Metrics

| Instrument | Type | Tags | Meaning |
|---|---|---|---|
| `actioncache.requests` | counter | `namespace`, `status` (`hit`/`miss`) | Cache lookups |
| `actioncache.operation.duration` | histogram, ms | `namespace`, `operation`, `outcome` (`ok`/`error`/`cancelled`) | One backend operation |
| `actioncache.evictions` | counter | `namespace` | Namespace evictions |
| `actioncache.single_flight.coalesced` | counter | `namespace` | Requests served by another's in-flight execution |

### Per request, or per operation

`actioncache.requests` and `actioncache.evictions` are recorded **once per request**, by the
filters.

The other two are per **backend operation**, so a [layered chain](../operations/layering)
or a single-flight re-check contributes more than one measurement for a single request.

That distinction matters if you are deriving a hit ratio: use `actioncache.requests`, which
counts requests. A counter recorded per backend read would give you a ratio of reads.

### The namespace tag

Every `namespace` tag carries the **unresolved template** — `Account:{id}`, never
`Account:42`. The resolved form is per-resource, which as a metric dimension is unbounded
cardinality: one time series per account.

### The outcome tag

`actioncache.operation.duration` records on **every** path, not just success. A backend that
hangs until the operation timeout elapses is exactly what the histogram exists to expose, so
failures and abandonments are sampled alongside successes. Without the `outcome` tag a
timeout would be indistinguishable from a slow success.

## Traces

Spans cover each backend operation and each refresh replay.

| Span | Tags |
|---|---|
| `ActionCache GetAsync` | `actioncache.namespace`, `actioncache.operation`, `actioncache.hit` |
| `ActionCache SetAsync` | `actioncache.namespace`, `actioncache.operation` |
| `ActionCache RemoveKey` | `actioncache.namespace`, `actioncache.operation` |
| `ActionCache EvictNamespace` | `actioncache.namespace`, `actioncache.operation` |
| `ActionCache GetKeysAsync` | `actioncache.namespace`, `actioncache.operation` |
| `ActionCache RefreshAsync` | `actioncache.namespace`, `actioncache.operation` |
| `ActionCache RefreshReplay` | `actioncache.operation` |

A degraded operation marks **its own span** as an error — not the ambient one. If nothing
subscribes to ActionCache's activity source, the current activity is the incoming request
span, and marking that would report a request which returned `200` as failed simply because
a cache read degraded exactly as fail-open intends.

`RefreshReplay` carries no namespace tag. It is a request path rather than a namespace, and
a path is unbounded in the same way a resolved namespace is.

## Logs

Beyond metrics and traces, the library logs:

- Degraded and fail-closed operations, at `Warning`, with the exception.
- Cache hits, misses, sets, evictions and refreshes, at `Debug`.
- Filter-level conditions the cache layer cannot see — a key that could not be built, a
  result that was not cacheable — at `Debug`.
- Refresh outcomes: a per-namespace summary, and each skipped or failed entry.
- Single-flight coalescing and lock timeouts.

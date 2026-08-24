---
title: Resilience
weight: 1
---

## Fail-open by default

If a backend throws while serving a request, the error is logged at `Warning` and the
operation degrades — to a cache miss for reads, to a no-op for writes, eviction and refresh.
The request still succeeds, uncached.

Caching is an enhancement, not a hard dependency: a Redis outage should slow an application
down, not take it down.

## Fail-closed

To make backend failures reach the caller instead:

```csharp
builder.Services.AddActionCache(options =>
{
    options.UseRedisCache("localhost:6379");
    options.FailClosed();
});
```

The original exception is rethrown with its stack trace intact.

## Timeouts

Fail-open catches exceptions, but it cannot bound a backend that **hangs** rather than
throws — and a hung read is worse than a failed one, because the request waits on it.

```csharp
builder.Services.AddActionCache(options =>
{
    options.UseRedisCache("localhost:6379");
    options.UseOperationTimeout(TimeSpan.FromMilliseconds(250));
});
```

An elapsed timeout is treated as a backend failure: degraded under fail-open, propagated
under fail-closed. **No timeout is configured by default.**

## Cancellation

Every `IActionCache` operation takes a `CancellationToken`, and the filters pass
`HttpContext.RequestAborted`. A client that disconnects stops backend work in flight.

A cancelled request always propagates its `OperationCanceledException` — it is **never**
degraded into a cache miss, even under fail-open. Degrading it would mean carrying on doing
work for an answer nobody is waiting for.

The distinction the decorator draws:

| Situation | Fail-open | Fail-closed |
|---|---|---|
| Backend throws | degrade | rethrow |
| Operation timeout elapsed | degrade | rethrow |
| **Caller's token cancelled** | **rethrow** | **rethrow** |

`IMemoryCache` is synchronous and StackExchange.Redis's `IDatabase` takes no token, so those
two check for cancellation before dispatching. SQL Server and Cosmos forward the token.

## Error responses are not cached

Only successful results are stored. A `4xx` or `5xx` leaves the cache untouched, so an
outage cannot be captured and then served back after it has passed.

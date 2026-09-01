---
title: Stampede protection
weight: 2
---

When a hot entry expires, every concurrent request misses at once and they all run the
action. ActionCache coalesces them: the first request executes while the rest wait, then
re-read the cache and reuse what it stored.

This is **on by default**.

## Opting out

Turn it off per endpoint when the action has per-request side effects that must not be
skipped:

```csharp
[HttpGet("forecasts")]
[ActionCache(Namespace = "Forecasts", SingleFlight = false)]
public IActionResult Get() => Ok(_forecasts);
```

## In-process by default

Coalescing is per process, matching .NET's `HybridCache`. With several instances running,
each executes the action once.

## Across instances

```csharp
builder.Services.AddActionCache(options =>
{
    options.UseRedisCache("localhost:6379");
    options.UseDistributedSingleFlight();
});
```

This requires **Redis or SQL Server** — Redis is preferred when both are registered — and
throws at startup if neither is present, rather than silently degrading to in-process
coalescing.

Every cache miss then costs a lock round-trip to the backend, which is why it is not the
default.

## Timing out

A waiter blocks for at most `ActionCacheSingleFlightOptions.WaitTimeout` (default 10
seconds). If the lock cannot be acquired in that time the request executes **uncoalesced**
rather than failing — consistent with the [fail-open](../resilience) stance.

`LeaseDuration` (default 30 seconds) is how long the leader may hold the lock before other
callers may assume it died. It must comfortably exceed the slowest action the cache fronts:
if the lease expires while the leader is still running, a waiter acquires the lock and runs
the action too — the stampede this exists to prevent.

Only backends whose locks carry a time-to-live enforce the lease — that is Redis, where it
is the lock key's TTL. The in-process semaphore and SQL Server's session-scoped
`sp_getapplock` hold until released, and a process that dies releases its lock by exiting.

Configuration is validated at startup: both values must be positive, and the lease must be
longer than the wait. Otherwise a caller that waited the full timeout would always find the
lease expired, and every slow request would stampede.

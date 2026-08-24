---
title: Memory
weight: 1
---

In-process caching over `IMemoryCache`. Included in the `ActionCache` package — no extra
install.

```csharp
builder.Services.AddActionCache(options =>
{
    options.UseMemoryCache(memory => memory.SizeLimit = 10_000);
});
```

The delegate configures `MemoryCacheOptions` directly, so anything that type supports is
available here.

## Set a size limit

Nothing bounds an in-memory cache by default. With [per-user keys](../../caching/vary-by)
on by default, an authenticated endpoint holds one entry per user, so an unbounded cache
grows with your user count:

```csharp
options.UseMemoryCache(memory => memory.SizeLimit = 10_000);
```

## When to use it

- A single instance, where a shared store buys nothing.
- As the **first layer** of a chain, in front of Redis or SQL Server — a hit in the deeper
  layer is promoted into this one, so the round-trip is paid once. See
  [Layered backends](../../operations/layering).

## What it does not do

Entries are per process. With several instances running, each has its own cache, and an
eviction on one does not reach the others — use a shared backend when that matters.

`UseDistributedSingleFlight()` cannot be backed by this backend; it needs Redis or SQL
Server.

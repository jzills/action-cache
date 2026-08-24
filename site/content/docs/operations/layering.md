---
title: Layered backends
weight: 3
---

Register more than one backend and they form a chain, in registration order:

```csharp
builder.Services.AddActionCache(options =>
{
    options.UseMemoryCache(memory => memory.SizeLimit = 10_000);
    options.UseRedisCache("localhost:6379");
});
```

## Reads promote

A read is served by the first layer that has the entry. A hit in a **deeper** layer is
copied into the faster one on the way back, so a Memory + Redis stack stops paying the Redis
round-trip after the first request for a given key.

The promoted copy expires on the first layer's schedule, which may be shorter than the
authoritative layer's. That is the intended relationship: the first layer caches the second,
it does not replicate it.

## Writes and eviction reach every layer

A write goes to all layers. Eviction and [refresh](../refresh) work from the **union** of
every layer's keys, so an entry that exists only in the deepest store is still evicted or
refreshed.

## Choosing an order

Fastest first. Memory in front of a shared store is the usual arrangement: the shared store
is what makes the cache coherent across instances, and the memory layer is what stops most
requests reaching it.

## Cost

Each layer is a real backend call on a miss, so a three-layer chain costs three round-trips
before the action runs. That is also why the per-operation metrics record once per layer
while the per-request metrics record once — see [Observability](../../observability).

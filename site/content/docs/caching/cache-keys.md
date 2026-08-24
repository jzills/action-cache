---
title: Cache keys
weight: 2
---

## What a key is made of

A key has three components:

1. **Route values** — area, controller and action names, plus any route parameters.
2. **Action arguments** — the bound parameters the action was called with.
3. **Vary-by values** — everything [vary-by](../vary-by) resolved for this request.

Those are serialized together and hashed. The result is appended to
`ActionCache:{Namespace}`, where the namespace has already had any route tokens bound.

The third component is only written when it is non-empty, so an endpoint that varies by
nothing produces the same key it would have before vary-by existed.

## Keys are hashed

Keys are **SHA-256** over those three components. Nothing in the library needs to reverse a
key — refresh replays the request recorded on the entry itself rather than reconstructing
it from the key — so hashing costs nothing but readability in a debugger.

This matters because a key is not a private place. Anyone with read access to the cache
store can list keys; before hashing, a key spelled out every route value and argument that
produced it, which for a search endpoint meant the search terms.

## Reading keys while debugging

```csharp
builder.Services.AddActionCache(options =>
{
    options.UseMemoryCache(memory => { });
    options.UsePlaintextKeys();
});
```

{{< callout type="warning" >}}
Plaintext keys embed every route value and action argument that produced an entry — ids,
filters, search terms — in a form anyone with read access to the store can recover. Look at
what yours would contain before leaving this on outside development.
{{< /callout >}}

## What is not in a key

Request **headers** are never part of a key unless you name one through `VaryByHeader`, and
they are never recorded on the entry. Headers routinely carry credentials, and a cache
entry is not a safe place to keep them.

## What is stored under a key

The value is a rendered response — status code, content type and body — together with the
request line that produced it, which is what [refresh](../../operations/refresh) replays.

It is deliberately a flat record of primitives, serialized with `System.Text.Json` through
a source-generated context. Nothing in a stored payload names a type to construct, so a
cache entry cannot influence which types get instantiated when it is read back.

{{< callout type="warning" >}}
For an endpoint that takes a request body and does **not** vary by request, that body is
recorded on the entry so refresh can replay it. Hashing removed arguments from the *key*;
this puts the payload in the *value*. It is less exposed than before — a value is not
enumerable the way a key list is — but if a body carries anything sensitive, prefer
[eviction](../../operations/eviction) over refresh for that endpoint.
{{< /callout >}}

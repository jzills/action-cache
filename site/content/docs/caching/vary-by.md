---
title: Vary-by
weight: 3
---

Route values and action arguments alone are not enough to identify a response on an
endpoint that returns data belonging to the caller: two authenticated users produce the
same key, and the second is served the first one's response.

## Per-user is the default

If a request is authenticated, the caller's identity joins the key without being asked for:

```csharp
[HttpGet("me")]
[Authorize]
[ActionCache(Namespace = "Me")]          // already per-user
public IActionResult GetMe() => Ok(_repository.ForUser(User));
```

Anonymous requests are unaffected — there is no identity to separate them by, so they go on
sharing one entry.

To recover the shared entry for a response that genuinely does not depend on who asked:

```csharp
[ActionCache(Namespace = "Rates", VaryByUser = VaryByUserMode.Never)]
```

## Other dimensions

Each is a comma-separated list:

```csharp
[ActionCache(
    Namespace = "Catalog",
    VaryByHeader = "Accept-Language",
    VaryByQuery = "page,sort",
    VaryByClaim = "tenant_id")]
```

A named-but-absent header or query value is recorded as **empty rather than skipped**, so
`Accept-Language: en` and no `Accept-Language` at all cannot collide on one entry.

## Contributors

For anything the attributes cannot express — a tenant read from a subdomain, a feature-flag
cohort, a negotiated API version — implement a contributor:

```csharp
public class TenantKeyContributor : IActionCacheKeyContributor
{
    public ValueTask ContributeAsync(
        HttpContext httpContext,
        IDictionary<string, string?> varyByValues,
        CancellationToken cancellationToken)
    {
        varyByValues["tenant"] = httpContext.Request.Host.Host.Split('.')[0];
        return ValueTask.CompletedTask;
    }
}

builder.Services.AddActionCacheKeyContributor<TenantKeyContributor>();
```

Every registered contributor runs for every cached request. Values are stored sorted, so
the order contributors happen to run in cannot change the key.

## Two consequences worth knowing

### Cardinality

Per-user keys mean one entry per user per endpoint. On the memory backend, set a
`SizeLimit` so the cache cannot grow without bound:

```csharp
options.UseMemoryCache(memory => memory.SizeLimit = 10_000);
```

### Refresh skips varied entries

[Refresh](../../operations/refresh) works by replaying the request an entry was built from.
For an entry that varies by request context, replaying it would mean re-issuing **another
caller's** request — impersonating them to warm their cache entry. Rather than do that,
refresh skips those entries and logs that it did. They are kept fresh by ordinary expiry
instead.

Since `VaryByUserMode.Auto` is the default, that means every authenticated endpoint. If you
need refresh on such an endpoint, it has to opt out of varying by user — which is only
correct when the response really is the same for everyone.

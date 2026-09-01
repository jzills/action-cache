# ActionCache.Abstractions

The core contracts for [ActionCache](https://www.nuget.org/packages/ActionCache/) —
namespaced response caching for ASP.NET Core.

```bash
dotnet add package ActionCache.Abstractions
```

**Most applications do not need this package.** Install
[`ActionCache`](https://www.nuget.org/packages/ActionCache/) for the attributes, filters and
the in-memory backend, or one of the backend packages
([`ActionCache.Redis`](https://www.nuget.org/packages/ActionCache.Redis/),
[`ActionCache.SqlServer`](https://www.nuget.org/packages/ActionCache.SqlServer/),
[`ActionCache.AzureCosmos`](https://www.nuget.org/packages/ActionCache.AzureCosmos/)) —
each of them brings this one with it.

Reference it directly when you are **writing a cache backend of your own**, or when a
library needs to depend on the abstractions without pulling in an implementation.

## What is here

| Type | Purpose |
|---|---|
| `IActionCache` | The core contract: `GetAsync`, `SetAsync`, `RemoveAsync`, `RefreshAsync`, `GetKeysAsync` |
| `IActionCacheFactory` | Creates an `IActionCache` per namespace |
| `ActionCacheBase<TLock>` | Base class carrying the locking strategy a backend opts into |
| `Namespace` | The namespace primitive, including route-parameter templates |
| `ActionCacheEntryOptions` | Absolute and sliding expiration for a single entry |
| `CachedResponse` | The stored value: status code, content type, rendered body, and the request refresh replays |

This package takes no third-party dependencies — no Redis client, no SqlClient, no Cosmos
SDK, no Newtonsoft.

## Documentation

Full documentation: <https://jzills.github.io/action-cache/>
